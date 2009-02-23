// Copyright (c) 2005, Stefan Bodewig
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
// 
//     * Redistributions in binary form must reproduce the above
//       copyright notice, this list of conditions and the following
//       disclaimer in the documentation and/or other materials provided
//       with the distribution.
// 
//     * The name Stefan Bodewig must not be used to endorse or
//       promote products derived from this software without specific
//       prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text;

#if MSBUILD
using Microsoft.Build.Framework;
#endif

#if NANT
using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.Core.Tasks;
#endif

namespace de.samaflost.AntTask {

    public enum BuildTool { NAnt, MSBuild }

    /// <summary>
    /// Base class for NAnt and MSBuild tasks running Ant <seealso cref="http://ant.apache.org/"/>.
    /// This is the NAnt version of the task at the same time.
    /// </summary>
#if NANT
    [TaskName("ant")]
    public class BaseAnt : NAnt.Core.Task
#else
    public class BaseAnt
#endif
 {
        #region constructors
        public BaseAnt()
            : this(BuildTool.NAnt) {
        }

        protected BaseAnt(BuildTool t) {
            currentBuildTool = t;
        }
        #endregion

        #region task attributes
        #region the build file
        private static readonly string DEFAULT_BUILD_FILE = "build.xml";

        private string buildFile = DEFAULT_BUILD_FILE;

        /// <summary>
        /// The build file - defaults to build.xml.
        /// </summary>
#if NANT
        [TaskAttribute("buildFile")]
#endif
        public string BuildFile {
            get {
                return buildFile;
            }
            set {
                buildFile = value;
            }
        }
        #endregion

        #region targets to execute
        private ArrayList targetNames = new ArrayList();

        /// <summary>
        /// The target(s) to execute
        /// </summary>
        /// <remarks>This setter will only be used by MSBuild</remarks>
        public string AntTargets {
            get {
                return string.Join(",", (string[]) targetNames.ToArray(typeof(string)));
            }
            set {
                foreach (string t in value.Split(',')) {
                    targetNames.Add(t);
                }
            }
        }

#if NANT
        /// <summary>
        /// The target(s) to execute
        /// </summary>
        [BuildElementArray("antTarget")]
        public AntTarget[] AntTargetElements {
            set {
                foreach (AntTarget t in value) {
                    targetNames.Add(t.TargetName);
                }
            }
            get {
                AntTarget[] r = new AntTarget[targetNames.Count];
                for (int i = 0; i < r.Length; i++) {
                    r[i] = new AntTarget();
                    r[i].TargetName = targetNames[i] as string;
                }
                return r;
            }
        }
#endif
        #endregion

        #region properties to set
        private StringDictionary propertyTable = new StringDictionary();

        /// <summary>
        /// The properties(s) to define
        /// </summary>
        /// <remarks>This setter will only be used by MSBuild</remarks>
        public string AntProperties {
            set {
                foreach (string pair in value.Split(';')) {
                    string[] p = pair.Split('=');
                    if (p.Length == 2) {
                        propertyTable[p[0]] = p[1];
                    }
                    else {
                        preConditionsFailed = true;
                        Warn(string.Format("Property {0} is not in the correct format", p));
                    }
                }
            }
            get {
                StringBuilder sb = new StringBuilder();
                foreach (string p in propertyTable.Keys) {
                    if (sb.Length > 0) {
                        sb.Append(';');
                    }
                    sb.Append(p).Append('=').Append(propertyTable[p]);
                }
                return sb.ToString();
            }
        }

#if NANT
        /// <summary>
        /// The properties(s) to define
        /// </summary>
        [BuildElementArray("antProperty")]
        public AntProperty[] AntPropertyElements {
            set {
                foreach (AntProperty p in value) {
                    propertyTable[p.PropertyName] = p.Value;
                }
            }
            get {
                AntProperty[] p = new AntProperty[propertyTable.Count];
                int i = 0;
                foreach (string key in propertyTable.Keys) {
                    p[i] = new AntProperty();
                    p[i].PropertyName = key;
                    p[i++].Value = propertyTable[key];
                }
                return p;
            }
        }
#endif
        #endregion

        #region -lib path
        private string libPath;

        /// <summary>
        /// The path to use for the -lib argument
        /// </summary>
#if NANT
        [TaskAttribute("libPath")]
#endif
        public string LibPath {
            get {
                return libPath;
            }
            set {
                libPath = value;
            }
        }
        #endregion

        #region ANT_HOME
        private string antHome;

        /// <summary>
        /// Where to find Ant.
        /// </summary>
#if NANT
        [TaskAttribute("antHome")]
#endif
        public string AntHome {
            get {
                return antHome == null ? Environment.GetEnvironmentVariable("ANT_HOME") : antHome;
            }
            set {
                antHome = value;
            }
        }
        #endregion

        #region JAVA_HOME
        private string javaHome;

        /// <summary>
        /// Where to find Java.
        /// </summary>
#if NANT
        [TaskAttribute("javaHome")]
#endif
        public string JavaHome {
            get {
                return javaHome == null ? Environment.GetEnvironmentVariable("JAVA_HOME") : javaHome;
            }
            set {
                javaHome = value;
            }
        }
        #endregion

        #endregion

        #region personality checks
        private BuildTool currentBuildTool;
        protected bool RunningInMSBuild {
            get {
                return currentBuildTool == BuildTool.MSBuild;
            }
        }
        protected bool RunningInNAnt {
            get {
                return currentBuildTool == BuildTool.NAnt;
            }
        }
        #endregion

        #region Logging
        protected void Debug(string p) {
#if MSBUILD
            if (RunningInMSBuild) {
                GenericMSBuildLog(p, MessageImportance.Low);
            }
#endif
#if NANT
            if (RunningInNAnt) {
                GenericNAntLog(p, Level.Debug);
            }
#endif
        }

        protected void Log(string p) {
#if MSBUILD
            if (RunningInMSBuild) {
                GenericMSBuildLog(p, MessageImportance.Normal);
            }
#endif
#if NANT
            if (RunningInNAnt) {
                GenericNAntLog(p, Level.Info);
            }
#endif
        }

        protected void Warn(string p) {
#if MSBUILD
            if (RunningInMSBuild) {
                GenericMSBuildLog(p, MessageImportance.High);
            }
#endif
#if NANT
            if (RunningInNAnt) {
                GenericNAntLog(p, Level.Error);
            }
#endif
        }
        #endregion

        #region Build Tool specific stuff
#if MSBUILD
        #region MSBuild ITask Members

        private IBuildEngine buildEngine;
        public IBuildEngine BuildEngine {
            get {
                return buildEngine;
            }
            set {
                buildEngine = value;
            }
        }

        private ITaskHost hostObject;
        public ITaskHost HostObject {
            get {
                return hostObject;
            }
            set {
                hostObject = value;
            }
        }

        #endregion

        #region support stuff
        private static readonly string HELP_KEYWORD = string.Empty;
        private static readonly string MESSAGE_SENDER = "ant task";

        private void GenericMSBuildLog(string message, MessageImportance i) {
            BuildEngine.LogMessageEvent(new BuildMessageEventArgs(message, HELP_KEYWORD, MESSAGE_SENDER, i));
        }
        #endregion
#endif

#if NANT
        #region NAnt Task abstract method
        protected override void ExecuteTask() {
            if (!RunAnt()) {
                throw new BuildException("ant task failed");
            }
        }
        #endregion

        #region support stuff
        private void GenericNAntLog(string message, Level l) {
            Log(l, message);
        }
        #endregion
#endif
        #endregion

        #region do the real work
        private bool preConditionsFailed = false;
        protected bool RunAnt() {
            if (preConditionsFailed) {
                return false;
            }
            if (AntHome == null) {
                Warn(string.Format("Can't determine ANT_HOME, please set the {0} attribute or the ANT_HOME environment variable", RunningInMSBuild ? "AntHome" : "antHome"));
                return false;
            }
            FileInfo antLauncherJar = new FileInfo(Path.Combine(AntHome, "lib" + Path.DirectorySeparatorChar + "ant-launcher.jar"));
            if (!antLauncherJar.Exists) {
                Warn(string.Format("'{0}' calculated from ANT_HOME '{1}' doesn't exist", antLauncherJar.FullName, AntHome));
                return false;
            }
            string javaExecutable = LocateJava();
            return Run(javaExecutable, antLauncherJar.FullName) == 0;
        }

        /// <summary>
        /// tries to locate the Java executable, replicates quite a bit of Ant's wrapper script logic
        /// </summary>
        /// <returns>the absolute path of the Java executable, if found, "java" or "java.exe" otherwise</returns>
        private string LocateJava() {
            bool isDos = Path.PathSeparator == ';';
            string java = null;
            if (JavaHome != null) {
                if (!isDos) {
                    // AIX likes to hide IBM's JDK in strange places
                    FileInfo fi = new FileInfo(Path.Combine(JavaHome, "jre" + Path.DirectorySeparatorChar + "sh" + Path.DirectorySeparatorChar + "java"));
                    if (fi.Exists) {
                        java = fi.FullName;
                    }
                    else {
                        fi = new FileInfo(Path.Combine(JavaHome, "bin" + Path.DirectorySeparatorChar + "java"));
                        if (fi.Exists) {
                            java = fi.FullName;
                        }
                    }
                }
                else {
                    FileInfo fi = new FileInfo(Path.Combine(JavaHome, "bin" + Path.DirectorySeparatorChar + "java.exe"));
                    if (fi.Exists) {
                        java = fi.FullName;
                    }
                }
                if (java == null) {
                    Warn(string.Format("Couldn't locate java in '{0}', will rely on your PATH environment variable", JavaHome));
                }
            }
            else {
                Log("Can't determine JAVA_HOME, will rely on your PATH environment variable");
            }
            if (java == null) {
                java = "java" + (isDos ? ".exe" : "");
            }
            return java;
        }

        private int Run(string java, string antJar) {
            try {
                ProcessStartInfo pi = new ProcessStartInfo(java);
                StringBuilder sb = new StringBuilder("-jar ");
                sb.Append(MaybeQuote(antJar));
                if (LibPath != null) {
                    sb.Append(" -lib ").Append(MaybeQuote(LibPath));
                }
                sb.Append(" -buildfile ").Append(MaybeQuote(BuildFile));
                foreach (string key in propertyTable.Keys) {
                    sb.Append(" ").Append(MaybeQuote(string.Format("-D{0}={1}", key, propertyTable[key])));
                }
                foreach (string target in targetNames) {
                    sb.Append(" ").Append(MaybeQuote(target));
                }
                pi.Arguments = sb.ToString();
                pi.UseShellExecute = false;
                pi.RedirectStandardOutput = true;
                Debug(string.Format("running {0} with args {1}", java, pi.Arguments));
                using (Process p = Process.Start(pi)) {
                    Log(p.StandardOutput.ReadToEnd());
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch (Exception e) {
                Warn(string.Format("Ant execution failed because of: '{0}'", e.ToString()));
                return -1;
            }
        }

        private static readonly char[] BAD_CHARS = new char[] { ' ', '\t', '\"' };
        /// <summary>
        /// puts quote around arg if arg contains whitespace
        /// </summary>
        /// <param name="arg">command line argument</param>
        /// <returns>arg or "arg"</returns>
        private string MaybeQuote(string arg) {
            if (arg.IndexOfAny(BAD_CHARS) >= 0) {
                if (arg.IndexOf('"') >= 0) {
                    return "'" + arg + "'";
                }
                else {
                    return "\"" + arg + "\"";
                }
            }
            else {
                return arg;
            }
        }
        #endregion
    }

#if MSBUILD
    /// <summary>
    /// MSBuild task to run Ant.
    /// </summary>
    public class Ant : BaseAnt, ITask {
        public Ant()
            : base(BuildTool.MSBuild) {
        }

        public new bool Execute() {
            return RunAnt();
        }
    }
#endif

#if NANT

    /// <summary>
    /// Nested element that specifies targets in the NAnt task running Ant.
    /// </summary>
    [ElementName("antTarget")]
    public class AntTarget : Element {
        private string name;
        /// <summary>
        /// target name.
        /// </summary>
        [TaskAttribute("name")]
        public string TargetName {
            get {
                return name;
            }
            set {
                name = value;
            }
        }
    }

    /// <summary>
    /// Nested element that specifies properties in the NAnt task running Ant.
    /// </summary>
    [ElementName("antProperty")]
    public class AntProperty : Element {
        private string name;
        /// <summary>
        /// property name.
        /// </summary>
        [TaskAttribute("name")]
        public string PropertyName {
            get {
                return name;
            }
            set {
                name = value;
            }
        }
        private string value;
        /// <summary>
        /// property value
        /// </summary>
        [TaskAttribute("value")]
        public string Value {
            get {
                return value;
            }
            set {
                this.value = value;
            }
        }
    }
#endif
}
