using System;
using System.Diagnostics;
using System.Text;

using Microsoft.Build.Framework;

namespace GodotWithFSharp.Build
{
    public sealed class Generate : ITask
    {
        public IBuildEngine BuildEngine { get; set; } = null!;
        public ITaskHost HostObject { get; set; } = null!;
        [Required] public string ToolAssembly { get; set; } = "";
        [Required] public ITaskItem[] Assemblies { get; set; } = Array.Empty<ITaskItem>();
        [Required] public string Namespace { get; set; } = "";
        [Required] public string OutputDirectory { get; set; } = "";
        [Required] public string Owner { get; set; } = "";
        public string DotNetHost { get; set; } = "";
        public bool AllowEmpty { get; set; }
        public bool AllowInputChange { get; set; }
        public bool AdoptExisting { get; set; }

        private void Error(string code, string message) =>
            BuildEngine.LogErrorEvent(new BuildErrorEventArgs(null, code, null, 0, 0, 0, 0, message, null, "GodotWithFSharp"));

        private void Message(string message) =>
            BuildEngine.LogMessageEvent(new BuildMessageEventArgs(message, null, "GodotWithFSharp", MessageImportance.High));

        public bool Execute()
        {
            var start = new ProcessStartInfo(string.IsNullOrEmpty(DotNetHost) ? "dotnet" : DotNetHost)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            start.ArgumentList.Add(ToolAssembly);
            foreach (var assembly in Assemblies)
            {
                start.ArgumentList.Add("--assembly");
                start.ArgumentList.Add(assembly.GetMetadata("FullPath"));
            }
            start.ArgumentList.Add("--namespace");
            start.ArgumentList.Add(Namespace);
            start.ArgumentList.Add("--output");
            start.ArgumentList.Add(OutputDirectory);
            start.ArgumentList.Add("--owner");
            start.ArgumentList.Add(Owner);
            if (AllowEmpty) start.ArgumentList.Add("--allow-empty");
            if (AllowInputChange) start.ArgumentList.Add("--allow-input-change");
            if (AdoptExisting) start.ArgumentList.Add("--adopt-existing");
            try
            {
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(120000))
                {
                    process.Kill(true);
                    process.WaitForExit();
                    Error("GFS202", "Code generation timed out.");
                    return false;
                }
                Message(output.GetAwaiter().GetResult());
                var error = errors.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                {
                    Error("GFS203", "Code generation failed: " + error);
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(error)) Message(error);
                return true;
            }
            catch (Exception error)
            {
                Error("GFS203", "Could not run code generation: " + error.Message);
                return false;
            }
        }
    }
}
