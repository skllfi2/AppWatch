using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: PatchXamlCompiler <path-to-XamlCompiler.exe>");
            return 1;
        }

        var path = args[0];
        var backup = path + ".bak";
        
        if (!File.Exists(backup))
        {
            File.Copy(path, backup, true);
            Console.WriteLine($"Backed up to {backup}");
        }

        using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var assembly = AssemblyDefinition.ReadAssembly(fs, new ReaderParameters { ReadingMode = ReadingMode.Immediate });
        var module = assembly.MainModule;

        // Find the entry point
        var entryPoint = assembly.EntryPoint;
        if (entryPoint == null)
        {
            Console.Error.WriteLine("No entry point found");
            return 1;
        }

        Console.WriteLine($"Found entry point: {entryPoint.FullName}");

        // Get the IL processor
        var il = entryPoint.Body.GetILProcessor();

        // Import required types and methods from the target assembly's references (mscorlib for .NET Framework)
                        var mscorlib = module.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib") 
                            ?? module.AssemblyReferences.FirstOrDefault(r => r.Name == "System.Private.CoreLib");
                
                        var cultureInfoTypeRef = mscorlib != null 
                            ? new TypeReference("System.Globalization", "CultureInfo", module, mscorlib)
                            : module.ImportReference(typeof(System.Globalization.CultureInfo));
                
                        var threadTypeRef = mscorlib != null
                            ? new TypeReference("System.Threading", "Thread", module, mscorlib)
                            : module.ImportReference(typeof(System.Threading.Thread));

                        // Get methods/properties from the resolved types
                        var cultureInfoDef = cultureInfoTypeRef.Resolve();
                        var threadDef = threadTypeRef.Resolve();

                        // Find the specific methods/properties we need
                        var getCultureInfoMethod = cultureInfoDef.Methods.First(m => m.Name == "GetCultureInfo" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String");
                        var defaultThreadCurrentCultureSetter = cultureInfoDef.Properties.First(p => p.Name == "DefaultThreadCurrentCulture").SetMethod;
                        var defaultThreadCurrentUICultureSetter = cultureInfoDef.Properties.First(p => p.Name == "DefaultThreadCurrentUICulture").SetMethod;
                        var currentThreadProperty = threadDef.Properties.First(p => p.Name == "CurrentThread").GetMethod;
                        var currentCultureSetter = threadDef.Properties.First(p => p.Name == "CurrentCulture").SetMethod;
                        var currentUICultureSetter = threadDef.Properties.First(p => p.Name == "CurrentUICulture").SetMethod;

                        // Import the method references into the target module
                        var getCultureInfoMethodRef = module.ImportReference(getCultureInfoMethod);
                        var defaultThreadCurrentCultureSetterRef = module.ImportReference(defaultThreadCurrentCultureSetter);
                        var defaultThreadCurrentUICultureSetterRef = module.ImportReference(defaultThreadCurrentUICultureSetter);
                        var currentThreadPropertyRef = module.ImportReference(currentThreadProperty);
                        var currentCultureSetterRef = module.ImportReference(currentCultureSetter);
                        var currentUICultureSetterRef = module.ImportReference(currentUICultureSetter);

        // Insert at the beginning of the method
        var firstInstr = entryPoint.Body.Instructions[0];

        // Push "en-US" string
                il.InsertBefore(firstInstr, il.Create(OpCodes.Ldstr, "en-US"));
                // Call CultureInfo.GetCultureInfo("en-US")
                il.InsertBefore(firstInstr, il.Create(OpCodes.Call, getCultureInfoMethodRef));
                // Duplicate the culture reference for multiple uses
                il.InsertBefore(firstInstr, il.Create(OpCodes.Dup));
                // Set CultureInfo.DefaultThreadCurrentCulture
                il.InsertBefore(firstInstr, il.Create(OpCodes.Call, defaultThreadCurrentCultureSetterRef));
                // Duplicate again
                il.InsertBefore(firstInstr, il.Create(OpCodes.Dup));
                // Set CultureInfo.DefaultThreadCurrentUICulture
                il.InsertBefore(firstInstr, il.Create(OpCodes.Call, defaultThreadCurrentUICultureSetterRef));
                // Get Thread.CurrentThread
                il.InsertBefore(firstInstr, il.Create(OpCodes.Call, currentThreadPropertyRef));
                // Duplicate
                il.InsertBefore(firstInstr, il.Create(OpCodes.Dup));
                // Set Thread.CurrentThread.CurrentCulture
                il.InsertBefore(firstInstr, il.Create(OpCodes.Callvirt, currentCultureSetterRef));
                // Set Thread.CurrentThread.CurrentUICulture
                il.InsertBefore(firstInstr, il.Create(OpCodes.Callvirt, currentUICultureSetterRef));

        // Write the modified assembly to a temp file first
                var tempPath = path + ".tmp";
                assembly.Write(tempPath);
                Console.WriteLine($"Patched to {tempPath}");
        
                // Close the stream before replacing
                fs.Dispose();
        
                // Replace original
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
                Console.WriteLine($"Patched {path}");
                return 0;
    }
}
