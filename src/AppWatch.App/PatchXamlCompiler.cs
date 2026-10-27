using System;
using System.Linq;
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
        
        if (!System.IO.File.Exists(backup))
        {
            System.IO.File.Copy(path, backup, true);
            Console.WriteLine($"Backed up to {backup}");
        }

        var assembly = AssemblyDefinition.ReadAssembly(path);
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

        // Create instructions to set the culture at the very beginning
        // CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
        // CultureInfo.DefaultThreadCurrentCulture = culture;
        // CultureInfo.DefaultThreadCurrentUICulture = culture;
        // Thread.CurrentThread.CurrentCulture = culture;
        // Thread.CurrentThread.CurrentUICulture = culture;

        var cultureInfoType = module.ImportReference(typeof(System.Globalization.CultureInfo));
        var threadType = module.ImportReference(typeof(System.Threading.Thread));
        var getCultureInfoMethod = module.ImportReference(typeof(System.Globalization.CultureInfo).GetMethod("GetCultureInfo", new[] { typeof(string) }));
        var defaultThreadCurrentCultureSetter = module.ImportReference(typeof(System.Globalization.CultureInfo).GetProperty("DefaultThreadCurrentCulture").SetMethod);
        var defaultThreadCurrentUICultureSetter = module.ImportReference(typeof(System.Globalization.CultureInfo).GetProperty("DefaultThreadCurrentUICulture").SetMethod);
        var currentThreadProperty = module.ImportReference(typeof(System.Threading.Thread).GetProperty("CurrentThread"));
        var currentCultureSetter = module.ImportReference(typeof(System.Threading.Thread).GetProperty("CurrentCulture").SetMethod);
        var currentUICultureSetter = module.ImportReference(typeof(System.Threading.Thread).GetProperty("CurrentUICulture").SetMethod);

        // Insert at the beginning of the method
        var firstInstr = entryPoint.Body.Instructions[0];

        // Push "en-US" string
        il.InsertBefore(firstInstr, il.Create(OpCodes.Ldstr, "en-US"));
        // Call CultureInfo.GetCultureInfo("en-US")
        il.InsertBefore(firstInstr, il.Create(OpCodes.Call, getCultureInfoMethod));
        // Duplicate the culture reference for multiple uses
        il.InsertBefore(firstInstr, il.Create(OpCodes.Dup));
        // Set CultureInfo.DefaultThreadCurrentCulture
        il.InsertBefore(firstInstr, il.Create(OpCodes.Call, defaultThreadCurrentCultureSetter));
        // Duplicate again
        il.InsertBefore(firstInstr, il.Create(OpCodes.Dup));
        // Set CultureInfo.DefaultThreadCurrentUICulture
        il.InsertBefore(firstInstr, il.Create(OpCodes.Call, defaultThreadCurrentUICultureSetter));
        // Get Thread.CurrentThread
        il.InsertBefore(firstInstr, il.Create(OpCodes.Call, currentThreadProperty.GetMethod));
        // Duplicate
        il.InsertBefore(firstInstr, il.Create(OpCodes.Dup));
        // Set Thread.CurrentThread.CurrentCulture
        il.InsertBefore(firstInstr, il.Create(OpCodes.Callvirt, currentCultureSetter));
        // Set Thread.CurrentThread.CurrentUICulture
        il.InsertBefore(firstInstr, il.Create(OpCodes.Callvirt, currentUICultureSetter));

        // Write the modified assembly
        assembly.Write(path);
        Console.WriteLine($"Patched {path}");
        return 0;
    }
}