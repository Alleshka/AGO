using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ago.Core.Git
{
    public class ProcessGitRunner : IGitRunner
    {
        public async Task<string> RunAsync(string arguments, string workingDirectory, CancellationToken ct = default)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                throw new GitException($"git {arguments} failed: {stderr.Trim()}");
            }

            return stdout;
        }
    }
}
