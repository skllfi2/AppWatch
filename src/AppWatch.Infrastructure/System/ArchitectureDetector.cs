using System.Runtime.InteropServices;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure.System;

public static class ArchitectureDetector
{
    public static SystemArchitecture Detect() =>
        RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => SystemArchitecture.X86_64,
            Architecture.X86 => SystemArchitecture.X86,
            Architecture.Arm64 => SystemArchitecture.Aarch64,
            Architecture.Arm => SystemArchitecture.Arm,
            _ => RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => SystemArchitecture.X86_64,
                Architecture.X86 => SystemArchitecture.X86,
                Architecture.Arm64 => SystemArchitecture.Aarch64,
                Architecture.Arm => SystemArchitecture.Arm,
                _ => SystemArchitecture.Unknown,
            },
        };
}
