fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/restart_manager.rs")
        .input_extern_file("src/pe_analyzer.rs")
        .input_extern_file("src/file_ops.rs")
        .csharp_class_name("NativeMethods")
        .csharp_namespace("AppWatch.Infrastructure.Native")
        .csharp_dll_name("appwatch_native")
        .generate_csharp_file("../../AppWatch.Infrastructure/Native/NativeMethods.g.cs")
        .unwrap();
}
