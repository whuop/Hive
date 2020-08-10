using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.IO;
using System.Diagnostics;
using System;
using System.Linq;
using Debug = UnityEngine.Debug;

namespace CreationKit.Editor
{
    [ScriptedImporter(1, ".proto")]
    public class ProtoImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            //EditorUtility.DisplayProgressBar("Compiling .proto files", $"Compiling {ctx.assetPath}", 0.5f);

            //try
            //{
                string protocPath = "e:/dev/protoc/protoc.exe";
                string filePath = Path.GetFullPath(ctx.assetPath);
                string sourcePath = Application.dataPath + "/";
                string outputPath = Path.GetDirectoryName(filePath) + "/";
                /*Debug.LogError(Path.Combine(outputPath, filePath));

                var lines = File.ReadLines(Path.Combine(outputPath, filePath));
                foreach (var line in lines)
                {;
                    if (line.TrimStart().StartsWith("message"))
                    {
                        int index = line.IndexOf("m");
                        string message = line.Remove(index, 7);
                        message = message.Trim();
                        Debug.Log("Message Name: " + message);
                    }
                }*/
                
                string protocArg = $"-I={sourcePath} --csharp_out={outputPath} {filePath}";
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = protocPath,
                        Arguments = protocArg,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.OutputDataReceived += (sender, args) => PrintLog(args.Data);
                process.ErrorDataReceived += (sender, args) => PrintError(args.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();
            //}
            /*catch(Exception e)
            {
                UnityEngine.Debug.LogError(e.Message);
            }
            finally
            {*/
              //  EditorUtility.ClearProgressBar();
            //}
        }

        public static void PrintLog(string data)
        {
            if (string.IsNullOrEmpty(data))
                return;
            UnityEngine.Debug.Log(data);
        }

        public static void PrintError(string data)
        {
            if (string.IsNullOrEmpty(data))
                return;
            UnityEngine.Debug.LogError(data);
        }
    }
}

