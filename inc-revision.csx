#! dotnet-script
#r "System.ValueTuple"
// increment .Net assembly revision
using System;
using System.IO;
using System.Text.RegularExpressions;

static string IDX(this IList<string> args, int index) => index >= args.Count ? null : args[index];
Match match_version(string s) => new Regex(@"^(\d+)\.(\d+)(?:\.(\d+)\.(\d+))?").Match(s);

if (Args.Count < 1)
{
    Console.WriteLine("usage: inc-revision.csx PROJECT-FILE CONFIG [ROOT-DIR]\n");
    Environment.Exit(0);
}

(string project_path, string config, string root_dir) = (Args.IDX(0), Args.IDX(1), Args.IDX(2));

var project_dir = new DirectoryInfo(project_path).Parent.FullName;
if (string.IsNullOrEmpty(root_dir))
{
    root_dir = project_dir;
}
var is_release = ("release" == config.ToLower());
// try
// {
//     Directory.SetCurrentDirectory(root_dir);
// }
// catch (System.Exception)
// {
//     throw new Exception($"{root_dir}");
// }


var proc = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = "rev-list HEAD --count .",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        CreateNoWindow = true,
        WorkingDirectory = root_dir
    }
};

try
{
    proc.Start();
}
catch (System.Exception)
{
    throw new Exception("Git not found");
}

string revision = proc.StandardOutput.ReadLine();
proc.WaitForExit();
if (proc.ExitCode != 0)
{
    throw new Exception("git.exe failed");
}
var prop_dir = Path.Combine(project_dir, "Properties");
var assembly_info = Path.Combine(project_dir, "Properties", "AssemblyInfo.cs");



var version_changed = false;
string tmp_filename;
StreamWriter tmp_output;
{
    var assembly_file = File.OpenText(assembly_info);
    tmp_filename = Path.Combine(prop_dir, Guid.NewGuid().ToString());

    tmp_output = File.CreateText(tmp_filename);

    string line;
    var r = new Regex(@"^\[assembly:\s*(Assembly(?:File)?Version)\s*\(""(.*?)""\)\]");

    while ((line = assembly_file.ReadLine()) != null)
    {
        if (r.IsMatch(line))
        {
            var match = r.Match(line);
            (string attr, string version) = (match.Groups[1].Value, match.Groups[2].Value);
            var mv = match_version(version);
            (int major, int minor, int build, int rev) = (Convert.ToInt32(mv.Groups[1].Value), Convert.ToInt32(mv.Groups[2].Value), Convert.ToInt32(mv.Groups[3].Value), Convert.ToInt32(mv.Groups[4].Value));
            if (is_release)
            {
                build += 1;
            }

            var new_version = $"{major}.{minor}.{build}.{revision}";
            line = $"[assembly: {attr} (\"{new_version}\")]";

            if (attr == "AssemblyVersion")
            {
                Console.WriteLine($"AssemblyVersion: {new_version}");
            }
            version_changed |= (version != new_version);
        }
        tmp_output.WriteLine(line);
    }

    assembly_file.Close();
}
tmp_output.Flush();
tmp_output.Close();
if (version_changed)
{
    try
    {
        File.Delete($"{assembly_info}~");
        File.Move(assembly_info, $"{assembly_info}~");
    }
    catch (System.Exception)
    {
        throw new Exception(assembly_info);
    }
    try
    {
        File.Move(tmp_filename, assembly_info);
    }
    catch (System.Exception)
    {
        throw new Exception(tmp_filename);
    }
}
else
{
    try
    {
        File.Delete($"{assembly_info}~");
        File.Delete(tmp_filename);
    }
    catch (System.Exception)
    {
        throw new Exception(tmp_filename);
    }
}