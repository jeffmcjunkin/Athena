using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using IronPython.Hosting;
using IronPython.Modules;
using IronPython.Runtime;
using System.Threading.Tasks;
using Athena.Plugins;

namespace Plugins
{
    public class Python : AthenaPlugin
    {
        public override string Name => "python";

        public override void Execute(Dictionary<string, string> args)
        {
            string pyfile = (string)args["pyfile"];
            RunPython(Base64Decode(pyfile).Result, (string)args["task-id"]);
        }

        static void RunPython(string PyFile, string taskid)// base64 decoded pyfile 
        {
            try
            {
                //Main program
                //Console.WriteLine("executing function runPython");
                string output = "";
                var eng = IronPython.Hosting.Python.CreateEngine();
                Assembly ass = Assembly.GetExecutingAssembly();
                var sysScope = eng.GetSysModule();
                var importer = new ResourceMetaPathImporter(Assembly.GetExecutingAssembly(), "Lib.zip");
                PythonList metaPath = (PythonList)sysScope.GetVariable("meta_path");
                metaPath.Add(importer);
                sysScope.SetVariable("meta_path", metaPath);
                //This for later Long running python tasks 
                var outputStream = new MemoryStream();
                var outputStreamWriter = new StreamWriter(outputStream);
                eng.Runtime.IO.SetOutput(outputStream, outputStreamWriter);
                //Run the file in eng
                eng.Execute(PyFile, sysScope);
                //This where we will call the execute function for the pys
                dynamic Execute = sysScope.GetVariable("Execute");
                output = Execute();
                //Write the ouput so athena can understand 
                PluginHandler.Write(output, taskid, true);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                PluginHandler.Write("Failed To Execute IronPy", taskid, true);

            }
        }
        private static string GetTargetsFromFile(byte[] b) // this should convert the file uploaded as b64 blob
        {
            return System.Text.Encoding.ASCII.GetString(b);

        }

        public static async Task<string> Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}
