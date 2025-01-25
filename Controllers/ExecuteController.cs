using Microsoft.AspNetCore.Mvc;
using PythonExecutionServer.Models;
using System.Diagnostics;
using System.IO;  // Added for File operations

[ApiController]
[Route("[controller]")]
public class ExecuteController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Execute([FromBody] ExecutionRequest request)
    {
        // Handle invalid JSON parsing errors
        if (request == null)
        {
            return BadRequest(new { error = "Invalid request format" });
        }

        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request format" });
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request format" });

        try
        {
            var result = await ExecutePythonCode(request.Code);

            if (result.ContainsKey("error"))
                return Ok(result);

            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task<Dictionary<string, string>> ExecutePythonCode(string code)
    {
        var tempFile = Path.GetTempFileName();
        await System.IO.File.WriteAllTextAsync(tempFile, code);  // Explicit namespace

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"{tempFile}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var timeoutTask = Task.Delay(2000);
        var exitTask = process.WaitForExitAsync();
        var completedTask = await Task.WhenAny(exitTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            process.Kill();
            return new Dictionary<string, string> { { "error", "execution timeout" } };
        }

#if WINDOWS
        process.Refresh();
        if (process.WorkingSet64 > 100 * 1024 * 1024)
        {
            return new Dictionary<string, string> { { "error", "memory limit exceeded" } };
        }
#endif

        var stdout = (await process.StandardOutput.ReadToEndAsync()).Trim();
        var stderr = (await process.StandardError.ReadToEndAsync()).Trim();

        System.IO.File.Delete(tempFile);  // Explicit namespace

        var response = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(stdout)) response["stdout"] = stdout;
        if (!string.IsNullOrEmpty(stderr)) response["stderr"] = stderr;

        return response;
    }
}