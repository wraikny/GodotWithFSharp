namespace GodotWithFSharp.Test

open System
open System.Diagnostics

module internal Processes =
  let run (timeout: int) workingDirectory executable (arguments: string seq) =
    let start = ProcessStartInfo(executable)
    start.WorkingDirectory <- workingDirectory
    start.UseShellExecute <- false
    start.CreateNoWindow <- true
    start.RedirectStandardOutput <- true
    start.RedirectStandardError <- true

    for argument in arguments do
      start.ArgumentList.Add argument

    use processHandle = Process.Start start
    let output = processHandle.StandardOutput.ReadToEndAsync()
    let errors = processHandle.StandardError.ReadToEndAsync()

    if not (processHandle.WaitForExit timeout) then
      processHandle.Kill(true)
      processHandle.WaitForExit()
      failwith $"Process timed out: {executable}\n{output.Result}\n{errors.Result}"

    processHandle.ExitCode, output.Result + errors.Result
