﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NppPluginNET;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Text.RegularExpressions;
using SnippetExecutor.Compilers;

namespace SnippetExecutor
{
    internal class Main
    {
        #region " Fields "

        internal const string PluginName = "SnippetExecutor";
        private static string iniFilePath = null;
        private static bool someSetting = false;

        private static frmMyDlg frmMyDlg = null;
        private static int idMyDlg = -1;
        private static Bitmap tbBmp = Properties.Resources.star;
        private static Bitmap tbBmp_tbTab = Properties.Resources.star_bmp;
        private static Icon tbIcon = null;

        #endregion

        #region " StartUp/CleanUp "

        internal static void CommandMenuInit()
        {
            StringBuilder sbIniFilePath = new StringBuilder(Win32.MAX_PATH);
            Win32.SendMessage(PluginBase.nppData._nppHandle, NppMsg.NPPM_GETPLUGINSCONFIGDIR, Win32.MAX_PATH,
                              sbIniFilePath);
            iniFilePath = sbIniFilePath.ToString();
            if (!Directory.Exists(iniFilePath)) Directory.CreateDirectory(iniFilePath);
            iniFilePath = Path.Combine(iniFilePath, PluginName + ".ini");
            someSetting = (Win32.GetPrivateProfileInt("SomeSection", "SomeKey", 0, iniFilePath) != 0);

            PluginBase.SetCommand(1, "Show Console", myDockableDialog);
            idMyDlg = 1;
            PluginBase.SetCommand(2, "Run Snippet", CompileSnippet, new ShortcutKey(false, true, false, Keys.F5));


        }

        internal static void SetToolBarIcon()
        {
            toolbarIcons tbIcons = new toolbarIcons();
            tbIcons.hToolbarBmp = tbBmp.GetHbitmap();
            IntPtr pTbIcons = Marshal.AllocHGlobal(Marshal.SizeOf(tbIcons));
            Marshal.StructureToPtr(tbIcons, pTbIcons, false);
            Win32.SendMessage(PluginBase.nppData._nppHandle, NppMsg.NPPM_ADDTOOLBARICON,
                              PluginBase._funcItems.Items[idMyDlg]._cmdID, pTbIcons);
            Marshal.FreeHGlobal(pTbIcons);
        }

        internal static void PluginCleanUp()
        {
            Win32.WritePrivateProfileString("SomeSection", "SomeKey", someSetting ? "1" : "0", iniFilePath);

        }

        #endregion

        #region " notifications "

        #endregion

        #region " Menu functions "

        internal static void myDockableDialog()
        {
            if (frmMyDlg == null || frmMyDlg.IsDisposed)
            {
                frmMyDlg = new frmMyDlg();

                using (Bitmap newBmp = new Bitmap(16, 16))
                {
                    Graphics g = Graphics.FromImage(newBmp);
                    ColorMap[] colorMap = new ColorMap[1];
                    colorMap[0] = new ColorMap();
                    colorMap[0].OldColor = Color.Fuchsia;
                    colorMap[0].NewColor = Color.FromKnownColor(KnownColor.ButtonFace);
                    ImageAttributes attr = new ImageAttributes();
                    attr.SetRemapTable(colorMap);
                    g.DrawImage(tbBmp_tbTab, new Rectangle(0, 0, 16, 16), 0, 0, 16, 16, GraphicsUnit.Pixel, attr);
                    tbIcon = Icon.FromHandle(newBmp.GetHicon());
                }

                NppTbData _nppTbData = new NppTbData();
                _nppTbData.hClient = frmMyDlg.Handle;
                _nppTbData.pszName = "SnippetExecutor Console";
                _nppTbData.dlgID = idMyDlg;
                _nppTbData.uMask = NppTbMsg.DWS_DF_CONT_BOTTOM; // | NppTbMsg.DWS_ICONTAB | NppTbMsg.DWS_ICONBAR;
                _nppTbData.hIconTab = (uint) tbIcon.Handle;
                _nppTbData.pszModuleName = PluginName;
                IntPtr _ptrNppTbData = Marshal.AllocHGlobal(Marshal.SizeOf(_nppTbData));
                Marshal.StructureToPtr(_nppTbData, _ptrNppTbData, false);

                debug = frmMyDlg.getIOToConsole();  //update the debug IO

                Win32.SendMessage(PluginBase.nppData._nppHandle, NppMsg.NPPM_DMMREGASDCKDLG, 0, _ptrNppTbData);
            }
            else
            {
                Win32.SendMessage(PluginBase.nppData._nppHandle, NppMsg.NPPM_DMMSHOW, 0, frmMyDlg.Handle);
            }
        }

        internal static void RunSnippet()
        {

        }

        internal static IO debug = new IONull();

        internal static void CompileSnippet()
        {
            Logger log = null;
            try
            {
                log = Logger.CreateLogger();
                try
                {
                    IntPtr currScint = PluginBase.GetCurrentScintilla();

                    myDockableDialog();
                    IO console = frmMyDlg.getIOToConsole();


                    log.Log("compile snippet");

                    int len = (int) Win32.SendMessage(currScint, SciMsg.SCI_GETSELTEXT, 0, 0);
                    StringBuilder text;

                    if (len > 1)
                    {
                        //a selection exists
                        text = new StringBuilder(len);
                        Win32.SendMessage(currScint, SciMsg.SCI_GETSELTEXT, 0, text);
                    }
                    else
                    {
                        //no selection, parse whole file
                        len = (int) Win32.SendMessage(currScint, SciMsg.SCI_GETTEXT, 0, 0);
                        text = new StringBuilder(len);
                        Win32.SendMessage(currScint, SciMsg.SCI_GETTEXT, len, text);
                    }


                    if (text.Length == 0)
                    {
                        console.writeLine("No Text");
                        return;
                    }

                    //create defaults
                    SnippetInfo info = new SnippetInfo();
                    info.language = LangType.L_TEXT;
                    int langtype = (int) LangType.L_TEXT;
                    Win32.SendMessage(PluginBase.nppData._nppHandle, NppMsg.NPPM_GETCURRENTLANGTYPE, 0, out langtype);
                    info.language = (LangType) langtype;
                    info.stdIO = console;
                    info.console = console;
                    info.preprocessed = text.ToString();
                    info.runCmdLine = String.Empty;
                    info.compilerCmdLine = String.Empty;
                    info.options = new Hashtable();

                    StringBuilder sb = new StringBuilder(Win32.MAX_PATH);
                    Win32.SendMessage(PluginBase.nppData._nppHandle, NppMsg.NPPM_GETCURRENTDIRECTORY, Win32.MAX_PATH, sb);
                    info.workingDirectory = sb.ToString();

                    //process overrides
                    try
                    {
                        PreprocessSnippet(ref info, text.ToString());
                    }
                    catch (Exception ex)
                    {
                        console.writeLine("\r\n\r\n--- SnippetExecutor " + DateTime.Now.ToShortTimeString() + " ---");
                        console.writeLine("Exception processing snippet");
                        Main.HandleException(console, ex);
                        return;
                    }

                    console = info.console;

                    console.writeLine("\r\n\r\n--- SnippetExecutor " + DateTime.Now.ToShortTimeString() + " ---");

                    foreach (DictionaryEntry pair in info.options)
                    {
                        console.writeLine(pair.Key.ToString() + ":" + pair.Value.ToString());
                    }

                    //get correct compiler for language
                    info.compiler = getCompilerForLanguage(info.language);

                    info.compiler.console = info.console;
                    info.compiler.stdIO = info.stdIO;
                    foreach (DictionaryEntry e in info.options)
                    {
                        info.compiler.options.Add(e.Key, e.Value);
                    }

                    Thread th = new Thread(
                        delegate()
                            {
                                Logger logInner = Logger.CreateLogger();
                                try
                                {
                                    console.writeLine("-- Generating source for snippet...");
                                    info.postprepared = info.compiler.PrepareSnippet(info.postprocessed);

                                    if (info.options.ContainsKey("source"))
                                    {
                                        IO writer = console;
                                        writer.writeLine();
                                        if (!String.IsNullOrEmpty((string) info.options["source"]))
                                        {
                                            string opt = (info.options["source"] as string);
                                            if (!String.IsNullOrEmpty(opt))
                                            {
                                                try
                                                {
                                                    writer = ioForOption(opt, info);
                                                }
                                                catch (Exception ex)
                                                {
                                                    console.writeLine("Cannot write to " + opt);
                                                    console.writeLine(ex.Message);
                                                    return;
                                                }
                                            }
                                        }
                                        writer.write(info.postprepared);
                                    }

                                    if (String.IsNullOrEmpty(info.postprepared)) return;

                                    info.compilerCmdLine = (string) info.options["compile"];
                                    console.writeLine("\r\n-- compiling source with options " + info.compilerCmdLine);
                                    info.executable = info.compiler.Compile(info.postprepared, info.compilerCmdLine);

                                    if (info.executable == null) return;

                                    EventHandler cancelDelegate = delegate(object sender, EventArgs e)
                                                                      {
                                                                          info.compiler.Cancel();
                                                                          console.write("-- Cancelling --");
                                                                      };
                                    frmMyDlg.CancelRunButtonClicked += cancelDelegate;

                                    info.compiler.workingDirectory = info.workingDirectory;

                                    info.runCmdLine = (string) info.options["run"];
                                    console.writeLine("-- running with options " + info.runCmdLine + " --");
                                    info.compiler.execute(info.executable, info.runCmdLine);
                                    console.writeLine("\r\n-- finished run --");

                                    frmMyDlg.CancelRunButtonClicked -= cancelDelegate;


                                }
                                catch (Exception ex)
                                {
                                    Main.HandleException(console, ex);
                                    return;
                                }
                                finally
                                {
                                    if (info.executable != null)
                                    {
                                        info.compiler.cleanup(info);
                                    }

                                    console.Dispose();
                                    info.stdIO.Dispose();
                                    logInner.Dispose();
                                }
                            }
                        );



                    th.Start();


                }
                catch (Exception ex)
                {

                    if (Main.debug != null)
                    {
                        Main.HandleException(Main.debug, ex);
                    }
                    else
                    {
                        MessageBox.Show(ex.ToString());
                    }
                    return;
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error creating logger: " + ex.ToString());
            }
            finally
            {
                if (log != null)
                    log.Dispose();
            }
            

        }

        private const int maxInnerExceptions = 4;
        private static void HandleException(IO console, Exception ex)
        {
            try{
                console.errLine("Exception! " + ex.ToString());
                Exception inner = ex.InnerException;
            }
            catch(Exception ex2)
            {
                MessageBox.Show(ex2.ToString() + "\r\nhappened when processing " + ex.Message);
            }
            //for (int i = 0; i < maxInnerExceptions; i++)
            //{
            //    if (inner == null)
            //        return;

            //    console.errLine("caused by " + inner.ToString());

            //    inner = inner.InnerException;
            //}
        }

        private static ISnippetCompiler getCompilerForLanguage(LangType langType)
        {
            switch (langType)
            {
                case LangType.L_TEXT:
                case LangType.L_CS:
                    return new CSharpCompiler();

                case LangType.L_VB:
                    return new VBCompiler();

                default:
                    throw new Exception("No compiler for language " + langType.ToString());
            }
        }

        static void PreprocessSnippet(ref SnippetInfo info, String snippetText)
        {
            string[] lines = snippetText.Split(new String[]{"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
            int snippetStart = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i].Trim();
                if (!String.IsNullOrEmpty(l))
                {
                    if (l.StartsWith(">>"))
                    {
                        l = l.Substring(2);
                        try
                        {
                            parseOption(ref info, l);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Exception parsing option >>" + l + " : " + ex.Message, ex);
                        }
                    }
                    else
                    {
                        snippetStart = i;
                        break;
                    }
                }
            }

            int snippetEnd = lines.Length;
            for (int i = snippetStart; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("--- SnippetExecutor"))
                {
                    snippetEnd = i;
                    break;
                }
            }

            StringBuilder sb = new StringBuilder();
            for (int i = snippetStart; i < snippetEnd; i++)
            {
                sb.AppendLine(lines[i]);
            }

            info.postprocessed = sb.ToString();

            return;
        }

        static void parseOption(ref SnippetInfo info, String option)
        {
            option.Trim();
            string[] cmdOps = option.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (cmdOps.Length == 0) return;
			String cmd = cmdOps[0].ToLower();
            switch (cmd)
            {
                case "lang":
                    if (cmdOps.Length < 2) throw new Exception("no language specified");
                    string lang = "L_" + cmdOps[1].Trim().ToUpper();
                    bool success = Enum.TryParse<LangType>(lang, out info.language);
                    break;

                case "out":
                    if (cmdOps.Length < 2) throw new Exception("no output specified");
                    info.stdIO = ioForOption(option.Substring(cmdOps[0].Length, option.Length - cmdOps[0].Length), info);
                    break;
					
				case "console":
                    if (cmdOps.Length < 2) throw new Exception("no output specified");
                    info.console = ioForOption(option.Substring(cmdOps[0].Length, option.Length - cmdOps[0].Length), info);
                    break;

                case "working":
                    if (cmdOps.Length < 2) throw new Exception("no directory specified");

                    string dir = cmd.Substring(cmd.Length).Trim();
                    if(!Directory.Exists(dir)) 
                        throw new Exception("directory " + Path.GetFullPath(dir) + " doesn't exist");
                    else
                        info.workingDirectory = dir;
                    break;
					
				default:
					//shove it in the hashtable
					string s = (string)info.options[cmd];
					if (! String.IsNullOrEmpty(s)){
                        if (option.Length > cmd.Length)
                        {
                            //multiple lines with the same option: append them to one line
                            info.options[cmd] = String.Concat(s, " ", option.Substring(cmd.Length).Trim());
                        }
					}
					else
					{
                        if (option.Length > cmd.Length)
                            info.options[cmd] = option.Substring(cmd.Length).Trim();
                        else
                            info.options[cmd] = String.Empty;
					}
					break;



            }
        }

        #endregion

        private static IO ioForOption(String option, SnippetInfo info)
        {
            option = option.TrimStart();
            
            if (String.IsNullOrEmpty(option))
            {
                throw new Exception("No IO destination specified");
            }

            if (option.StartsWith("console", StringComparison.OrdinalIgnoreCase))
            {
                return frmMyDlg.getIOToConsole();
            }
            else if (option.StartsWith("append", StringComparison.OrdinalIgnoreCase))
            {
                return new IOAppendCurrentDoc();
            }
            else if (option.StartsWith("insert", StringComparison.OrdinalIgnoreCase))
            {
                return new IOInsertAtPosition();
            }
            else if (option.StartsWith("new", StringComparison.OrdinalIgnoreCase))
            {
                return IONewDoc.NewDocFactory();
            }
            else if (option.StartsWith("file", StringComparison.OrdinalIgnoreCase))
            {
                string filename = option.Substring(4, option.Length - 4);
                if (!Path.IsPathRooted(filename))
                {
                    filename = Path.Combine(info.workingDirectory, filename);
                }

                //create an IO to that document
                return IOFileDoc.FileDocFactory(filename);

            }
            else
            {
                //TODO: new IOWriteDoc();
                throw new NotImplementedException("silent file");
            }
        }

        
    }

    

    public struct SnippetInfo
    {
        public ISnippetCompiler compiler;
        public IO stdIO;
		public IO console;

        public Hashtable options;

        public LangType language;

        /// <summary>
        /// The working directory of the snippet being run
        /// </summary>
        public String workingDirectory;

        /// <summary>
        /// The compiler command line options
        /// </summary>
        public String compilerCmdLine;
        /// <summary>
        /// The run-time command line options
        /// </summary>
        public String runCmdLine;

        /// <summary>
        /// The initial input text
        /// </summary>
        public String preprocessed;
        /// <summary>
        /// The snippet text after parsing the options
        /// </summary>
        public String postprocessed;
        /// <summary>
        /// The valid source code prepared for the snippet
        /// </summary>
        public String postprepared;
        /// <summary>
        /// The object returned by the compiler's Compile method.  
        /// Generally a reference to the compiled, executable source code
        /// </summary>
        public Object executable;
    }


    public static class TemplateLoader
    {
        private static Hashtable templates = new Hashtable();

        public static string getTemplate(LangType language)
        {
            lock (templates.SyncRoot)
            {

                if (templates[language] == null)
                {
                    string path = @"plugins/SnippetExecutor/templates/" + language.ToString() + ".txt";
                    string ret = loadTemplate(path);
                    if (ret == null)
                    {
                        throw new Exception("No template for " + System.IO.Path.GetFullPath(path));    
                    }
                    templates[language] = ret;
                    return ret;
                }
                else
                {
                    return (string) templates[language];
                }
            }
        }

        private static string loadTemplate(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                using (StreamReader reader = new StreamReader(File.OpenRead(path)))
                {
                    return reader.ReadToEnd();
                }
            }catch(IOException)
            {
                return null;
            }
        }

        public static void clearTemplates()
        {
            lock(templates.SyncRoot)
            {
                templates.Clear();
            }
        }

        private const string tagStart = "${SnippetExecutor.";
        private static readonly Regex tagRegex = new Regex(@"\${SnippetExecutor.(?<tagName>[a-zA-Z0-9]+)}");

        public static string insertSnippet(string tagName, string snippet, string template)
        {
            string toReplace = String.Concat(tagStart, tagName, "}");

            template = template.Replace(toReplace, snippet);
            
            return template;
        }

        public static string removeOtherSnippets(string template)
        {
            template = tagRegex.Replace(template, new MatchEvaluator(m => String.Empty));

            return template;
        }

    }


    class Logger : IDisposable
    {
        string filename;
        StreamWriter writer;

        int refs = 0;

        protected Logger(string filename)
        {
            this.filename = filename;

            this.writer = new StreamWriter(File.Open(filename, FileMode.Append, FileAccess.Write));
            writer.WriteLine("Logger for SnippetExecutor " + DateTime.Now.ToString());
        }

        static Hashtable loggers = new Hashtable();
        public static Logger getLogger()
        {
            string filename = "plugins/SnippetExecutor/log_" + DateTime.Now.ToString("d-M-yy") + ".log";
            lock (loggers)
            {
                if (loggers.ContainsKey(filename) && loggers[filename] != null)
                {
                    Logger ret = (Logger)loggers[filename];
                    return ret;
                }
                else
                {
                    throw new Exception("No logger");
                }
            }
        }

        public static Logger CreateLogger()
        {
            string filename = "plugins/SnippetExecutor/log_" + DateTime.Now.ToString("d-M-yy") + ".log";
            lock (loggers)
            {
                if (loggers.ContainsKey(filename) && loggers[filename] != null)
                {
                    Logger ret = (Logger)loggers[filename];
                    ret.refs++;
                    return ret;
                }
                else
                {
                    Logger ret = new Logger(filename);
                    ret.refs++;
                    loggers[filename] = ret;
                    return ret;
                }
            }

        }

        public void Log(string msg)
        {
            msg = DateTime.Now.ToShortTimeString() + " -- " + msg;
            writer.WriteLine(msg);
        }



        public void Dispose()
        {
            lock (loggers)
            {
                refs--;
                if (refs <= 0)
                {
                    writer.Dispose();
                    loggers.Remove(filename);
                }
            }
        }

        
    }
}