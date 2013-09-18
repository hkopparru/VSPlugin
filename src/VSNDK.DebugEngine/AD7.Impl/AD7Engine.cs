//* Copyright 2010-2011 Research In Motion Limited.
//*
//* Licensed under the Apache License, Version 2.0 (the "License");
//* you may not use this file except in compliance with the License.
//* You may obtain a copy of the License at
//*
//* http://www.apache.org/licenses/LICENSE-2.0
//*
//* Unless required by applicable law or agreed to in writing, software
//* distributed under the License is distributed on an "AS IS" BASIS,
//* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//* See the License for the specific language governing permissions and
//* limitations under the License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Diagnostics;
using System.Threading;
using VSNDK.Parser;
//using VSNDK.AddIn;
using VSNDK.Package;

using NameValueCollection = System.Collections.Specialized.NameValueCollection;
using NameValueCollectionHelper = VSNDK.Package.NameValueCollectionHelper;
using System.Collections;
using System.Windows.Forms;

namespace VSNDK.DebugEngine
{
    
    /// <summary>
    /// AD7Engine is the primary entrypoint object for the sample engine. 
    /// 
    /// It implements:
    ///
    /// IDebugEngine2: This interface represents a debug engine (DE). It is used to manage various aspects of a debugging session, 
    /// from creating breakpoints to setting and clearing exceptions. (http://msdn.microsoft.com/en-us/library/bb145310.aspx).
    ///
    /// IDebugEngineLaunch2: Used by a debug engine (DE) to launch and terminate programs. 
    /// (http://msdn.microsoft.com/en-us/library/bb146230.aspx).
    ///
    /// IDebugProgram3: This interface represents a program that is running in a process. Since this engine only debugs one process at a time and each 
    /// process only contains one program, it is implemented on the engine. (http://msdn.microsoft.com/en-us/library/bb145884.aspx).
    ///
    /// IDebugEngineProgram2: This interface provides simultanious debugging of multiple threads in a debuggee.
    /// (http://msdn.microsoft.com/en-us/library/bb145128.aspx).
    /// 
    /// IDebugSymbolSettings100: (http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.debugger.interop.idebugsymbolsettings100.aspx).
    ///
    /// Comments:
    /// Process "Is a container for a set of programs".
    /// "A debug engine (DE) attaches to a program, and not to a process or a thread".
    /// Program "Is a container for both a set of threads and a set of modules.".
    /// </summary>
    [ComVisible(true)]
    [Guid("904AA6E0-942C-4D11-9094-7BAAEB3EE4B9")]
    public class AD7Engine : IDebugEngine2, IDebugEngineLaunch2, IDebugProgram3, IDebugEngineProgram2, IDebugSymbolSettings100
    {

        /// <summary>
        /// Used to send events to the debugger. Some examples of these events are thread create, exception thrown, module load.
        /// </summary>
        EngineCallback m_engineCallback = null;
        internal EngineCallback Callback
        {
            get { return m_engineCallback; }
        }


        /// <summary>
        /// This object manages breakpoints in the sample engine.
        /// </summary>
        protected BreakpointManager m_breakpointManager = null;
        public BreakpointManager BPMgr
        {
            get { return m_breakpointManager; }
        }


        /// <summary>
        /// This object manages debug events in the engine.
        /// </summary>
        protected EventDispatcher m_eventDispatcher = null;
        public EventDispatcher eDispatcher
        {
            get { return m_eventDispatcher; }
        }


        /// <summary>
        /// A unique identifier for the program being debugged.
        /// </summary>
        Guid m_programGUID;

        /// <summary>
        /// A module loaded in the debuggee process to the debugger. It is always null because the AD7Module class is 
        /// not implemented yet.
        /// </summary>
        internal AD7Module m_module = null;

        /// <summary>
        /// A process running on a port. A process "Is a container for a set of programs".
        /// </summary>
        public AD7Process m_process = null;

        /// <summary>
        /// A program that can be debugged.
        /// </summary>
        public AD7ProgramNode m_progNode = null;

        /// <summary>
        /// A program that is running in a process.
        /// </summary>
        internal IDebugProgram2 m_program = null;

        /// <summary>
        /// TRUE whenever a thread is created/ended, so the debug engine can update the m_threads data structure.
        /// </summary>
        public bool _updateThreads = false;

        /// <summary>
        /// Array of threads of the program that is being debugged.
        /// </summary>
        private AD7Thread[] m_threads = null;
        public AD7Thread[] thread
        {
            get { return m_threads; }
        }

        /// <summary>
        /// The position in the m_threads array that corresponds to the current thread.
        /// </summary>
        public int _currentThreadIndex = -1;

        
        /// <summary>
        /// Allows IDE to show current position in a source file
        /// </summary>
        public AD7DocumentContext m_docContext = null;


        /// <summary>
        /// Used to avoid race condition when there are conditional breakpoints and the user hit break all. Both events will pause 
        /// the Debug Engine, but the one related to conditional breakpoint can resume execution. If both events happens at the same 
        /// time, the Debug Engine could resume the execution instead of pausing it.
        /// </summary>
        public ManualResetEvent m_running = new ManualResetEvent(true);

        /// <summary>
        /// 
        /// </summary>
        public int interruptCounter = 0;

        /// <summary>
        /// 
        /// </summary>
        public bool receivedInterruptSignal = false;

        /// <summary>
        /// Used to avoid race condition when there are conditional breakpoints and a breakpoint is hit. This situation is similar to the above one.
        /// </summary>
        public ManualResetEvent m_updatingConditionalBreakpoint = new ManualResetEvent(true); 


        /// <summary>
        /// This is the engine GUID of the VSNDK debug engine. It needs to be changed here and in the registration when creating a 
        /// new engine.
        /// </summary>
        public const string Id = "{E5A37609-2F43-4830-AA85-D94CFA035DD2}";


        /// <summary>
        /// Keeps track of debug engine states
        /// </summary>
        public enum DE_STATE
        {                
            DESIGN_MODE = 0,
            RUN_MODE,
            BREAK_MODE,
            STEP_MODE,
            DONE
        }
        public DE_STATE m_state = 0;

        
        /// <summary>
        /// Constructor.
        /// </summary>
        public AD7Engine()
        {
            m_breakpointManager = new BreakpointManager(this);            
            m_state = DE_STATE.DESIGN_MODE;
        }


        /// <summary>
        /// Destructor.
        /// </summary>
        ~AD7Engine()
        {
            // Transition DE state
            m_state = AD7Engine.DE_STATE.DONE;
        }

        #region IDebugEngine2 Members


        /// <summary>
        /// Attach the debug engine to a program. (http://msdn.microsoft.com/en-us/library/bb145136.aspx)
        /// </summary>
        /// <param name="rgpPrograms"> Represent programs to be attached to. These are port programs. </param>
        /// <param name="rgpProgramNodes"> Represent program nodes, one for each program. The program nodes in this array represent 
        /// the same programs as in pProgram. The program nodes are given so that the DE can identify the programs to attach to. </param>
        /// <param name="aCeltPrograms"> Number of programs and/or program nodes in the pProgram and rgpProgramNodes arrays. </param>
        /// <param name="ad7Callback"> The IDebugEventCallback2 object to be used to send debug events to the SDM. </param>
        /// <param name="dwReason">  A value from the ATTACH_REASON enumeration that specifies the reason for attaching these programs. </param>
        /// <returns> If successful, returns S_OK; otherwise, returns an error code. </returns>
        int IDebugEngine2.Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes, uint aCeltPrograms, IDebugEventCallback2 ad7Callback, enum_ATTACH_REASON dwReason)
        {

            if (aCeltPrograms != 1)
            {
                System.Diagnostics.Debug.Fail("VSNDK Debugger only supports one debug target at a time.");
                throw new ArgumentException();
            }

            try
            {
                EngineUtils.RequireOk(rgpPrograms[0].GetProgramId(out m_programGUID));

                m_program = rgpPrograms[0];

                // It is NULL when the user attached the debugger to a running process (by using Attach to Process UI). When that 
                // happens, some objects must be instantiated, as well as GDB must be launched. Those ones are instantiated/launched
                // by LaunchSuspended and ResumeProcess methods when debugging an open project.
                if (this.m_engineCallback == null) 
                {
                    VSNDK.Package.ControlDebugEngine.isDebugEngineRunning = true;
                    m_engineCallback = new EngineCallback(this, ad7Callback);

                    AD7ProgramNodeAttach pnt = (AD7ProgramNodeAttach)m_program;
                    m_process = pnt.m_process;
                    AD7Port port = pnt.m_process._portAttach;
                    string publicKeyPath = Environment.GetEnvironmentVariable("AppData") + @"\BlackBerry\bbt_id_rsa.pub";
                    string progName = pnt.m_programName.Substring(pnt.m_programName.LastIndexOf('/') + 1);

                    string exePath = "";
                    string processesPaths = "";
                    System.IO.StreamReader readProcessesPathsFile = null;
                    // Read the file ProcessesPath.txt to try to get the file location of the executable file.
                    try
                    {
                        readProcessesPathsFile = new System.IO.StreamReader(System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Research In Motion\ProcessesPath.txt");
                        processesPaths = readProcessesPathsFile.ReadToEnd();
                        readProcessesPathsFile.Close();
                    }
                    catch (Exception e)
                    {
                        processesPaths = "";
                    }

                    string searchProgName = progName + "_" + port.m_isSimulator;
                    int begin = processesPaths.IndexOf(searchProgName + ":>");

                    if (begin != -1)
                    {
                        begin += searchProgName.Length + 2;
                        int end = processesPaths.IndexOf("\r\n", begin);

                        exePath = processesPaths.Substring(begin, end - begin) + progName;
                    }
                    else
                    {
                        exePath = "CannotAttachToRunningProcess";
                    }

                    exePath = exePath.Replace("\\", "\\\\\\\\");

                    if (GDBParser.LaunchProcess(pnt.m_programID, exePath, port.m_IP, port.m_isSimulator, port.m_toolsPath, publicKeyPath, port.m_password))
                    {
                        if (exePath == "CannotAttachToRunningProcess")
                        {
                            MessageBox.Show(progName + " is attached to the debugger. However, to be able to debug your application, you must build and deploy it from this computer first.", "No executable file with symbols found.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        m_eventDispatcher = new EventDispatcher(this);
                        m_module = new AD7Module();
                        m_progNode = new AD7ProgramNode(m_process._processGUID, m_process._processID, exePath, new Guid(AD7Engine.Id));
                        AddThreadsToProgram();
                    }
                    else
                    {
                        GDBParser.exitGDB();
                        VSNDK.Package.ControlDebugEngine.isDebugEngineRunning = false;
                        return VSConstants.E_FAIL;
                    }
                }
                AD7EngineCreateEvent.Send(this);
                AD7ProgramCreateEvent.Send(this);
                AD7ModuleLoadEvent.Send(this, m_module, true);
                AD7LoadCompleteEvent.Send(this, currentThread());

                // If the reason for attaching is ATTACH_REASON_LAUNCH, the DE needs to send the IDebugEntryPointEvent2 event.
                // See http://msdn.microsoft.com/en-us/library/bb145136%28v=vs.100%29.aspx
                if (dwReason == enum_ATTACH_REASON.ATTACH_REASON_LAUNCH)
                {
                    AD7EntryPointEvent ad7Event = new AD7EntryPointEvent();
                    Guid riidEvent = new Guid(AD7EntryPointEvent.IID);
                    uint attributes = (uint)enum_EVENTATTRIBUTES.EVENT_STOPPING | (uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS;
                    int rc = ad7Callback.Event(this, null, m_program, currentThread(), ad7Event, ref riidEvent, attributes);
                    Debug.Assert(rc == VSConstants.S_OK);
                }
            }
            catch (Exception e)
            {
                return EngineUtils.UnexpectedException(e);
            }
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Requests that all programs being debugged by this DE stop execution the next time one of their threads attempts to run.
        /// This is normally called in response to the user clicking on the pause button in the debugger. When the break is complete, 
        /// an AsyncBreakComplete event will be sent back to the debugger. (http://msdn.microsoft.com/en-us/library/bb145141.aspx)
        /// </summary>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngine2.CauseBreak()
        {
            m_running.WaitOne();

            if (m_state == DE_STATE.DESIGN_MODE || m_state == DE_STATE.BREAK_MODE)
            {
                interruptCounter += 1;
            }
            // If not already broken, send the interrupt
            else if (m_state != DE_STATE.STEP_MODE)
            {
                bool signalReceived;
                if (EventDispatcher.m_GDBRunMode == true)
                {
                    HandleProcessExecution.m_mre.Reset();

                    receivedInterruptSignal = false;

                    // Sends the GDB command that interrupts the background execution of the target.
                    // (http://sourceware.org/gdb/onlinedocs/gdb/GDB_002fMI-Program-Execution.html)
                    GDBParser.addGDBCommand(@"-exec-interrupt");
//                    m_state = AD7Engine.DE_STATE.BREAK_MODE;

                    // Ensure the process is interrupted before returning                
                    signalReceived = HandleProcessExecution.m_mre.WaitOne(2000);

                    if (VSNDK.Package.ControlDebugEngine.isDebugEngineRunning)
                        HandleProcessExecution.m_mre.Reset();
                }
            }

            m_running.Set();

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Called by the SDM to indicate that a synchronous debug event, previously sent by the DE to the SDM,
        /// was received and processed. The only event the VSNDK Debug Engine sends in this fashion is Program Destroy.
        /// It responds to that event by shutting down the engine. (http://msdn.microsoft.com/en-us/library/bb160915.aspx)
        /// </summary>
        /// <param name="eventObject"> Represents the previously sent synchronous event from which the debugger should now continue. </param>
        /// <returns> If successful, returns S_OK; otherwise, returns an error code. </returns>
        int IDebugEngine2.ContinueFromSynchronousEvent(IDebugEvent2 eventObject)
        {
            try
            {
                if (eventObject is AD7ProgramDestroyEvent)
                {
                    resetStackFrames();
                    m_process.Detach();
                    m_process = null;

                    m_program.Detach();
                    m_program = null;

                    m_engineCallback = null;
                    m_breakpointManager = null;
                    m_docContext = null;
                    m_eventDispatcher = null;
                    m_module = null;
                    m_progNode = null;
                    m_programGUID = Guid.Empty;

                    m_threads = null;
                    GC.Collect();
                }
                else
                {
                    Debug.Fail("Unknown synchronous event");
                }
            }
            catch (Exception e)
            {
                return EngineUtils.UnexpectedException(e);
            }
            
            if (m_eventDispatcher != null)
                m_eventDispatcher.continueExecution();

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Creates a pending breakpoint in the Debug Engine. A pending breakpoint is essentially a collection of all the information 
        /// needed to bind a breakpoint to code. (http://msdn.microsoft.com/en-us/library/bb147033.aspx)
        /// </summary>
        /// <param name="pBPRequest"> An IDebugBreakpointRequest2 object that describes the pending breakpoint to create. </param>
        /// <param name="ppPendingBP"> Returns an IDebugPendingBreakpoint2 object that represents the pending breakpoint. </param>
        /// <returns> If successful, returns S_OK; otherwise, returns an error code. </returns>
        int IDebugEngine2.CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP)
        {
            Debug.Assert(m_breakpointManager != null);
            ppPendingBP = null;

            try
            {
                m_breakpointManager.CreatePendingBreakpoint(pBPRequest, out ppPendingBP);
            }
            catch (Exception e)
            {
                return EngineUtils.UnexpectedException(e);
            }

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Informs a DE that the program specified has been atypically terminated and that the DE should clean up all references 
        /// to the program and send a program destroy event. (http://msdn.microsoft.com/en-us/library/bb145073.aspx)
        /// </summary>
        /// <param name="pProgram"> An IDebugProgram2 object that represents the program that has been atypically terminated. </param>
        /// <returns> AD7_HRESULT.E_PROGRAM_DESTROY_PENDING. </returns>
        int IDebugEngine2.DestroyProgram(IDebugProgram2 pProgram)
        {
            // Tell the SDM that the engine knows that the program is exiting, and that the
            // engine will send a program destroy. We do this because the Win32 debug api will always
            // tell us that the process exited, and otherwise we have a race condition.

            return (AD7_HRESULT.E_PROGRAM_DESTROY_PENDING);
        }


        /// <summary>
        /// Gets the GUID of the DE. (http://msdn.microsoft.com/en-us/library/bb145079.aspx)
        /// </summary>
        /// <param name="guidEngine"> Returns the GUID of the DE. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngine2.GetEngineId(out Guid guidEngine)
        {
            guidEngine = new Guid(Id);
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Removes the list of exceptions the IDE has set for a particular run-time architecture or language. The VSNDK debug engine 
        /// does not support exceptions in the debuggee so this method is not actually implemented.
        /// (http://msdn.microsoft.com/en-us/library/bb145134.aspx)
        /// </summary>
        /// <param name="guidType"> Either the GUID for the language or the GUID for the debug engine that is specific to a run-time 
        /// architecture. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngine2.RemoveAllSetExceptions(ref Guid guidType)
        {
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Removes the specified exception so it is no longer handled by the debug engine. The VSNDK debug engine does not support 
        /// exceptions in the debuggee so this method is not actually implemented. (http://msdn.microsoft.com/en-us/library/bb161697.aspx)
        /// </summary>
        /// <param name="pException"> An EXCEPTION_INFO structure that describes the exception to be removed. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngine2.RemoveSetException(EXCEPTION_INFO[] pException)
        {
            // The VSNDK debug engine will always stop on all exceptions.

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Specifies how the DE should handle a given exception. The VSNDK debug engine does not support exceptions in the debuggee 
        /// so this method is not actually implemented. (http://msdn.microsoft.com/en-us/library/bb162170.aspx)
        /// </summary>
        /// <param name="pException"> An EXCEPTION_INFO structure that describes the exception and how to debug it. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngine2.SetException(EXCEPTION_INFO[] pException)
        {           
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Sets the locale of the DE. This method is called by the session debug manager (SDM) to propagate the locale settings 
        /// of the IDE so that strings returned by the DE are properly localized. The sample engine is not localized so this is 
        /// not implemented. (http://msdn.microsoft.com/en-us/library/bb161784.aspx)
        /// </summary>
        /// <param name="wLangID"> Specifies the language locale. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngine2.SetLocale(ushort wLangID)
        {
            return VSConstants.S_OK;
        }


        /// <summary>
        /// A metric is a registry value used to change a debug engine's behavior or to advertise supported functionality. 
        /// This method can forward the call to the appropriate form of the Debugging SDK Helpers function, SetMetric.
        /// (http://msdn.microsoft.com/en-us/library/bb161968.aspx)
        /// </summary>
        /// <param name="pszMetric"> The metric name. </param>
        /// <param name="varValue"> Specifies the metric value. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngine2.SetMetric(string pszMetric, object varValue)
        {
            // The VSNDK debug engine does not need to understand any metric settings.
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Sets the registry root currently in use by the DE. Different installations of Visual Studio can change where their 
        /// registry information is stored. This allows the debugger to tell the engine where that location is. 
        /// (http://msdn.microsoft.com/en-us/library/bb161800.aspx)
        /// </summary>
        /// <param name="pszRegistryRoot"> The registry root to use. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngine2.SetRegistryRoot(string pszRegistryRoot)
        {
            // The VSNDK debug engine does not read settings from the registry.
            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugEngineLaunch2 Members


        /// <summary>
        /// Determines if a process can be terminated. (http://msdn.microsoft.com/en-us/library/bb146673.aspx)
        /// </summary>
        /// <param name="process"> An IDebugProcess2 object that represents the process to be terminated. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngineLaunch2.CanTerminateProcess(IDebugProcess2 process)
        {                        
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Launches a process by means of the debug engine. (http://msdn.microsoft.com/en-us/library/bb146223.aspx)
        /// </summary>
        /// <param name="pszServer"> The name of the machine in which to launch the process. Use a null value to specify the local 
        /// machine. Or it should be the name of the server? </param>
        /// <param name="port"> The IDebugPort2 interface representing the port that the program will run in. </param>
        /// <param name="exe"> The name of the executable to be launched. </param>
        /// <param name="args"> The arguments to pass to the executable. May be a null value if there are no arguments. </param>
        /// <param name="dir"> The name of the working directory used by the executable. May be a null value if no working directory is 
        /// required. </param>
        /// <param name="env"> Environment block of NULL-terminated strings, followed by an additional NULL terminator. </param>
        /// <param name="options"> The options for the executable. </param>
        /// <param name="launchFlags"> Specifies the LAUNCH_FLAGS for a session. </param>
        /// <param name="hStdInput"> Handle to an alternate input stream. May be 0 if redirection is not required. </param>
        /// <param name="hStdOutput"> Handle to an alternate output stream. May be 0 if redirection is not required. </param>
        /// <param name="hStdError"> Handle to an alternate error output stream. May be 0 if redirection is not required. </param>
        /// <param name="ad7Callback"> The IDebugEventCallback2 object that receives debugger events. </param>
        /// <param name="process"> Returns the resulting IDebugProcess2 object that represents the launched process. </param>
        /// <returns> If successful, returns S_OK; otherwise, returns an error code. </returns>
        int IDebugEngineLaunch2.LaunchSuspended(string pszServer, IDebugPort2 port, string exe, string args, string dir, string env, string options, enum_LAUNCH_FLAGS launchFlags, uint hStdInput, uint hStdOutput, uint hStdError, IDebugEventCallback2 ad7Callback, out IDebugProcess2 process)
        {            
            Debug.Assert(m_programGUID == Guid.Empty);

            process = null;

            try
            {
                VSNDK.Package.ControlDebugEngine.isDebugEngineRunning = true;
                m_engineCallback = new EngineCallback(this, ad7Callback);

                // Read arguments back from the args string
                var nvc = new NameValueCollection();
                NameValueCollectionHelper.LoadFromString(nvc, args);

                string pid = nvc.GetValues("pid")[0];
                string exePath = exe;
                string targetIP = nvc.GetValues("targetIP")[0];
                bool isSimulator = Convert.ToBoolean(nvc.GetValues("isSimulator")[0]);
                string toolsPath = nvc.GetValues("ToolsPath")[0];
                string publicKeyPath = nvc.GetValues("PublicKeyPath")[0];

                string password = null;
                string[] passwordArray = nvc.GetValues("Password");
                if (passwordArray != null)
                    password = passwordArray[0];

                if (GDBParser.LaunchProcess(pid, exePath, targetIP, isSimulator, toolsPath, publicKeyPath, password))
                {
                    process = m_process = new AD7Process(this, port);
                    m_eventDispatcher = new EventDispatcher(this);
                    m_programGUID = m_process._processGUID;
                    m_module = new AD7Module();
//                    m_progNode = new AD7ProgramNode(m_process.PhysID, pid, exePath, new Guid(AD7Engine.Id));
                    m_progNode = new AD7ProgramNode(m_process._processGUID, pid, exePath, new Guid(AD7Engine.Id));
                    AddThreadsToProgram();

                    AD7EngineCreateEvent.Send(this);

                    return VSConstants.S_OK;
                }
                else
                {
                    GDBParser.exitGDB();
                    VSNDK.Package.ControlDebugEngine.isDebugEngineRunning = false;
                    return VSConstants.E_FAIL;
                }
            }
            catch (Exception e)
            {
                return EngineUtils.UnexpectedException(e);
            }
        }


        /// <summary>
        /// Resume a process launched by IDebugEngineLaunch2.LaunchSuspended. (http://msdn.microsoft.com/en-us/library/bb146261.aspx)
        /// </summary>
        /// <param name="process"> An IDebugProcess2 object that represents the process to be resumed. </param>
        /// <returns> An IDebugProcess2 object that represents the process to be resumed. </returns>
        int IDebugEngineLaunch2.ResumeProcess(IDebugProcess2 process)
        {
            Debug.Assert(m_engineCallback != null);

            try
            {
                var xProcess = process as AD7Process;
                if (xProcess == null)
                {
                    return VSConstants.E_INVALIDARG;
                }

                // Send a program node to the SDM. This will cause the SDM to turn around and call IDebugEngine2.Attach
                // which will complete the hookup with AD7
                IDebugPort2 port = null;
                EngineUtils.RequireOk(process.GetPort(out port));
                
                IDebugDefaultPort2 defaultPort = (IDebugDefaultPort2)port;
                
                IDebugPortNotify2 portNotify = null;
                EngineUtils.RequireOk(defaultPort.GetPortNotify(out portNotify));

                EngineUtils.RequireOk(portNotify.AddProgramNode(m_progNode));

                Callback.OnModuleLoad(m_module);

                Callback.OnThreadStart(currentThread());
            }
            catch (Exception e)
            {
                return EngineUtils.UnexpectedException(e);
            }
            return VSConstants.S_OK;
        }


        /// <summary>
        /// This function is used to terminate a process. (http://msdn.microsoft.com/en-us/library/bb162314.aspx)
        /// The debugger will call IDebugEngineLaunch2::CanTerminateProcess before calling this method.
        /// </summary>
        /// <param name="process">  An IDebugProcess2 object that represents the process to be terminated. </param>
        /// <returns> VSConstants.S_OK. </returns>
        int IDebugEngineLaunch2.TerminateProcess(IDebugProcess2 process)
        {
            m_state = DE_STATE.DONE;
            CauseBreak();
            if (eDispatcher != null)
            {
                eDispatcher.killProcess();
                eDispatcher.endDebugSession(0);
            }

            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugProgram2 Members


        /// <summary>
        /// Determines if a debug engine (DE) can detach from the program. (http://msdn.microsoft.com/en-us/library/bb161967.aspx)
        /// </summary>
        /// <returns> VSConstants.S_OK. </returns>
        public int CanDetach()
        {
            // The VSNDK debug engine always supports detach
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Requests that the program stop execution the next time one of its threads attempts to run. The debugger calls CauseBreak 
        /// when the user clicks on the pause button in VS. The debugger should respond by entering breakmode. 
        /// (http://msdn.microsoft.com/en-us/library/bb145018.aspx)
        /// </summary>
        /// <returns> VSConstants.S_OK. </returns>
        public int CauseBreak()
        {
            return ((IDebugEngine2)this).CauseBreak();
        }


        /// <summary>
        /// Continues running this program from a stopped state. Any previous execution state (such as a step) is preserved, and 
        /// the program starts executing again. (http://msdn.microsoft.com/en-us/library/bb162148.aspx)
        /// </summary>
        /// <param name="pThread"> An IDebugThread2 object that represents the thread. </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int Continue(IDebugThread2 pThread)
        {
            m_eventDispatcher.continueExecution();

            return VSConstants.S_OK;            
        }


        /// <summary>
        /// Detaches a debug engine from the program. (http://msdn.microsoft.com/en-us/library/bb146228.aspx)
        /// </summary>
        /// <returns> VSConstants.S_OK. </returns>
        public int Detach()
        {            
            CauseBreak();
            if (m_breakpointManager != null)
                m_breakpointManager.ClearBoundBreakpoints(); // TODO: Check if active bound BP list needs to be updated too?
            
            if (eDispatcher != null)
                eDispatcher.endDebugSession(0);
            
            return VSConstants.S_OK;            
        }


        /// <summary>
        /// Retrieves a list of the code contexts for a given position in a source file. Not implemented. 
        /// (http://msdn.microsoft.com/en-us/library/bb145902.aspx)
        /// </summary>
        /// <param name="pDocPos"> An object representing an abstract position in a source file known to the IDE. </param>
        /// <param name="ppEnum"> Returns an IEnumDebugCodeContexts2 object that contains a list of the code contexts. </param>
        /// <returns> Not implemented. </returns>
        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum)
        {
            throw new Exception("The method or operation is not implemented.");
        }


        /// <summary>
        /// Retrieves a list of the code paths for a given position in a source file. EnumCodePaths is used for the step-into specific 
        /// feature -- right click on the current statement and decide which function to step into. This is not something that the 
        /// VSNDK debug engine supports. (http://msdn.microsoft.com/en-us/library/bb162326.aspx)
        /// </summary>
        /// <param name="hint"> The word under the cursor in the Source or Disassembly view in the IDE. </param>
        /// <param name="start"> An IDebugCodeContext2 object representing the current code context. </param>
        /// <param name="frame"> An IDebugStackFrame2 object representing the stack frame associated with the current breakpoint. </param>
        /// <param name="fSource"> Nonzero (TRUE) if in the Source view, or zero (FALSE) if in the Disassembly view. </param>
        /// <param name="pathEnum"> Returns an IEnumCodePaths2 object containing a list of the code paths. </param>
        /// <param name="safetyContext"> Returns an IDebugCodeContext2 object representing an additional code context to be set as a 
        /// breakpoint in case the chosen code path is skipped. This can happen in the case of a short-circuited Boolean expression, 
        /// for example. </param>
        /// <returns> VSConstants.E_NOTIMPL. </returns>
        public int EnumCodePaths(string hint, IDebugCodeContext2 start, IDebugStackFrame2 frame, int fSource, out IEnumCodePaths2 pathEnum, out IDebugCodeContext2 safetyContext)
        {
            pathEnum = null;
            safetyContext = null;
            return VSConstants.E_NOTIMPL;
        }


        /// <summary>
        /// Retrieves a list of the modules that this program has loaded and is executing. 
        /// (http://msdn.microsoft.com/en-us/library/bb146980.aspx)
        /// </summary>
        /// <param name="ppEnum"> Returns an IEnumDebugModules2 object that contains a list of the modules. </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int EnumModules(out IEnumDebugModules2 ppEnum)
        {
            ppEnum = new AD7ModuleEnum(new[] { m_module });
            return VSConstants.S_OK;            
        }


        /// <summary>
        /// Retrieves a list of the threads that are running in the program. (http://msdn.microsoft.com/en-us/library/bb145110.aspx)
        /// </summary>
        /// <param name="ppEnum"> Returns an IEnumDebugThreads2 object that contains a list of the threads. </param>
        /// <returns> If successful, returns S_OK; otherwise, returns an error code. </returns>
        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            AD7Thread[] listThreads = null;
            int currentThread = GetListOfThreads(out listThreads);

            // the following code seems to be weird but I had to update each field of this.m_process._threads because, when using
            // "this.m_process._threads = listThreads;" without having a new thread, VS starts to duplicate the existing threads 
            // and, as a consequence, the call stack entries too.

            if ((currentThread == -1) || (listThreads == null))
            {
                ppEnum = null;
                return VSConstants.S_FALSE;
            }

            if (listThreads.Length != this.m_threads.Length)
            {
                foreach (AD7Thread t in this.m_threads)
                {
                    AD7ThreadDestroyEvent.Send(this, 0, t);
                }
                this.m_threads = null;
                this.m_threads = new AD7Thread[listThreads.Length];
                this.m_threads = listThreads;
                this._currentThreadIndex = currentThread;
                foreach (AD7Thread t in this.m_threads)
                {
                    m_engineCallback.OnThreadStart(t);
                }
                ppEnum = new AD7ThreadEnum(this.m_threads);
            }
            else 
            {
                if (this._currentThreadIndex != currentThread)
                {
                    this._currentThreadIndex = currentThread;
                }

                for (int i = 0; i < listThreads.Length; i++)
                {
                    if (this.m_threads[i]._engine != listThreads[i]._engine)
                    {
                        this.m_threads[i]._engine = listThreads[i]._engine;
                    }
                    if (this.m_threads[i]._threadDisplayName != listThreads[i]._threadDisplayName)
                    {
                        this.m_threads[i]._threadDisplayName = listThreads[i]._threadDisplayName;
                    }
                    if (this.m_threads[i]._id != listThreads[i]._id)
                    {
                        this.m_threads[i]._id = listThreads[i]._id;
                    }
                    if (this.m_threads[i]._state != listThreads[i]._state)
                    {
                        this.m_threads[i]._state = listThreads[i]._state;
                    }
                    if (this.m_threads[i]._targetID != listThreads[i]._targetID)
                    {
                        this.m_threads[i]._targetID = listThreads[i]._targetID;
                    }
                    if (this.m_threads[i]._priority != listThreads[i]._priority)
                    {
                        this.m_threads[i]._priority = listThreads[i]._priority;
                    }
                    if (this.m_threads[i]._line != listThreads[i]._line)
                    {
                        this.m_threads[i]._line = listThreads[i]._line;
                    }
                    if (this.m_threads[i]._filename != listThreads[i]._filename)
                    {
                        if (listThreads[i]._filename == "")
                            this.m_threads[i]._filename = "";
                        else
                            this.m_threads[i]._filename = listThreads[i]._filename;
                    }
                } 

                ppEnum = new AD7ThreadEnum(this.m_threads);
            }
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Gets the program's properties. The VSNDK debug engine does not support this. 
        /// (http://msdn.microsoft.com/en-us/library/bb161801.aspx)
        /// </summary>
        /// <param name="ppProperty"> Returns an IDebugProperty2 object that represents the program's properties. </param>
        /// <returns> Not implemented. </returns>
        public int GetDebugProperty(out IDebugProperty2 ppProperty)
        {
            throw new Exception("The method or operation is not implemented.");
        }


        /// <summary>
        /// Gets the disassembly stream for this program or a part of this program.
        /// The sample engine does not support dissassembly so it returns E_NOTIMPL
        /// </summary>
        /// <param name="dwScope"> Specifies a value from the DISASSEMBLY_STREAM_SCOPE enumeration that defines the scope of the 
        /// disassembly stream.</param>
        /// <param name="codeContext"> An object that represents the position of where to start the disassembly stream. </param>
        /// <param name="disassemblyStream"> Returns an IDebugDisassemblyStream2 object that represents the disassembly stream. </param>
        /// <returns> VSConstants.E_NOTIMPL. </returns>
        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 codeContext, out IDebugDisassemblyStream2 disassemblyStream)
        {
            disassemblyStream = null;
            return VSConstants.E_NOTIMPL;
        }


        /// <summary>
        /// This method gets the Edit and Continue (ENC) update for this program. A custom debug engine always returns E_NOTIMPL
        /// </summary>
        /// <param name="update"> Returns an internal interface that can be used to update this program. </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int GetENCUpdate(out object update)
        {
            // The VSNDK debug engine does not participate in managed edit & continue.
            update = null;
            return VSConstants.S_OK;            
        }


        /// <summary>
        /// Gets the name and GUID of the debug engine running this program. (http://msdn.microsoft.com/en-us/library/bb145854.aspx)
        /// </summary>
        /// <param name="engineName"> Returns the name of the DE running this program. </param>
        /// <param name="engineGuid"> Returns the GUID of the DE running this program. </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int GetEngineInfo(out string engineName, out Guid engineGuid)
        {
            engineName = "VSNDK Debug Engine";
            engineGuid = new Guid(AD7Engine.Id);

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Retrieves the memory bytes occupied by the program. Not implemented. (http://msdn.microsoft.com/en-us/library/bb145291.aspx)
        /// </summary>
        /// <param name="ppMemoryBytes"> Returns an IDebugMemoryBytes2 object that represents the memory bytes of the program. </param>
        /// <returns> Not implemented. </returns>
        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            throw new Exception("The method or operation is not implemented.");
        }


        /// <summary>
        /// Gets the name of the program. The name returned by this method is always a friendly, user-displayable name that describes 
        /// the program. (http://msdn.microsoft.com/en-us/library/bb161279.aspx)
        /// </summary>
        /// <param name="programName"> Returns the name of the program. </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int GetName(out string programName)
        {
            if (this.m_progNode != null)
                programName = this.m_progNode.m_programName;
            else
                programName = null;
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Gets a GUID for this program. A debug engine (DE) must return the program identifier originally passed to the 
        /// IDebugProgramNodeAttach2::OnAttach or IDebugEngine2::Attach methods. This allows identification of the program 
        /// across debugger components. (http://msdn.microsoft.com/en-us/library/bb145581.aspx)
        /// </summary>
        /// <param name="guidProgramId"> Returns the GUID for this program. </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int GetProgramId(out Guid guidProgramId)
        {
            if (m_programGUID != null)
                guidProgramId = m_programGUID;
            else
                guidProgramId = m_process._processGUID;

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Performs a step. This method is deprecated. Use the IDebugProcess3::Step method instead. 
        /// (http://msdn.microsoft.com/en-us/library/bb162134.aspx)
        /// </summary>
        /// <param name="pThread"> An IDebugThread2 object that represents the thread being stepped. </param>
        /// <param name="sk"> A value from the STEPKIND enumeration that specifies the kind of step. </param>
        /// <param name="Step"> A value from the STEPUNIT enumeration that specifies the unit of step. </param>
        /// <returns> If successful, returns S_OK; otherwise, returns an error code. </returns>
        public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT Step)
        {
            // Don't allow stepping through unknown code because it can lead to future problems with stepping and break-all.
            if (VSNDK.DebugEngine.EventDispatcher.m_unknownCode)
            {
                m_state = AD7Engine.DE_STATE.STEP_MODE;
                m_eventDispatcher.continueExecution();                

                return VSConstants.S_OK;
            }

            if (sk == enum_STEPKIND.STEP_INTO)
            {
                // Equivalent to F11 hotkey.
                // Sends the GDB command that resumes the execution of the inferior program, stopping at the next instruction, diving 
                // into function if it is a function call. (http://sourceware.org/gdb/onlinedocs/gdb/GDB_002fMI-Program-Execution.html)
                GDBParser.addGDBCommand("-exec-step --thread " + this.m_threads[this._currentThreadIndex]._id);
            }
            else if (sk == enum_STEPKIND.STEP_OVER)
            { 
                // Equivalent to F10 hotkey.
                // Sends the GDB command that resumes the execution of the inferior program, stopping at the next instruction, but 
                // without diving into functions. (http://sourceware.org/gdb/onlinedocs/gdb/GDB_002fMI-Program-Execution.html)
                GDBParser.addGDBCommand("-exec-next --thread " + this.m_threads[this._currentThreadIndex]._id);
            }
            else if (sk == enum_STEPKIND.STEP_OUT)
            { 
                // Equivalent to Shift-F11 hotkey.
                if (eDispatcher.getStackDepth(this.m_threads[this._currentThreadIndex]._id) > 1)
                {
                    // Sends the GDB command that resumes the execution of the inferior program until the current function is exited.
                    // (http://sourceware.org/gdb/onlinedocs/gdb/GDB_002fMI-Program-Execution.html)
                    GDBParser.addGDBCommand("-exec-finish --thread " + this.m_threads[this._currentThreadIndex]._id + " --frame 0");
                }
                else
                {
                    // If this the only frame left, do a step-over.
                    // Sends the GDB command that resumes the execution of the inferior program, stopping at the next instruction, but 
                    // without diving into functions. (http://sourceware.org/gdb/onlinedocs/gdb/GDB_002fMI-Program-Execution.html)
                    GDBParser.addGDBCommand("-exec-next --thread " + this.m_threads[this._currentThreadIndex]._id);
                }
            }
            else if (sk == enum_STEPKIND.STEP_BACKWARDS)
            {
                return VSConstants.E_NOTIMPL;
            }
            else
            {
                m_engineCallback.OnStepComplete(); // Have to call this otherwise VS gets "stuck"
            }

            // Transition DE state                
            m_state = AD7Engine.DE_STATE.STEP_MODE;

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Terminates the program. (http://msdn.microsoft.com/en-us/library/bb145919.aspx)
        /// </summary>
        /// <returns> VSConstants.S_OK. </returns>
        public int Terminate()
        {
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Writes a dump to a file. Not implemented. (http://msdn.microsoft.com/en-us/library/bb145827.aspx)
        /// </summary>
        /// <param name="DUMPTYPE"> A value from the DUMPTYPE enumeration that specifies the type of dump, for example, short or 
        /// long. </param>
        /// <param name="pszDumpUrl"> The URL to write the dump to. Typically, this is in the form of file://c:\path\filename.ext, 
        /// but may be any valid URL. </param>
        /// <returns> VSConstants.E_NOTIMPL. </returns>
        public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl)
        {
            // The VSNDK debugger does not support creating or reading mini-dumps.
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugProgram3 Members


        /// <summary>
        /// Executes the debugger program. The thread is returned to give the debugger information on which thread the user is 
        /// viewing when executing the program. ExecuteOnThread is called when the SDM wants execution to continue and have 
        /// stepping state cleared. (http://msdn.microsoft.com/en-us/library/bb145596.aspx)
        /// </summary>
        /// <param name="pThread"> An IDebugThread2 object. </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int ExecuteOnThread(IDebugThread2 pThread)
        {
            m_eventDispatcher.continueExecution();

            return VSConstants.S_OK;            
        }

        #endregion

        #region IDebugEngineProgram2 Members

        
        /// <summary>
        /// Stops all threads running in this program. This method is called when this program is being debugged in a multi-program 
        /// environment. When a stopping event from some other program is received, this method is called on this program. The 
        /// implementation of this method should be asynchronous; that is, not all threads should be required to be stopped before 
        /// this method returns. The implementation of this method may be as simple as calling the IDebugProgram2::CauseBreak method 
        /// on this program. Not implemented. (http://msdn.microsoft.com/en-us/library/bb146567.aspx)
        ///
        /// The VSNDK debug engine only supports debugging native applications and therefore only has one program per-process
        /// </summary>
        /// <returns> VSConstants.E_NOTIMPL. </returns>
        public int Stop()
        {
            return VSConstants.E_NOTIMPL;
        }


        /// <summary>
        /// Allows (or disallows) expression evaluation to occur on the given thread, even if the program has stopped.
        /// WatchForExpressionEvaluationOnThread is used to cooperate between two different engines debugging the same process. 
        /// The VSNDK debug engine doesn't cooperate with other engines, so it has nothing to do here. 
        /// (http://msdn.microsoft.com/en-us/library/bb144913.aspx)
        /// </summary>
        /// <param name="pOriginatingProgram"> An IDebugProgram2 object representing the program that is evaluating an expression. </param>
        /// <param name="dwTid"> Specifies the identifier of the thread. </param>
        /// <param name="dwEvalFlags"> A combination of flags from the EVALFLAGS enumeration that specify how the evaluation is to be 
        /// performed. </param>
        /// <param name="pExprCallback"> An IDebugEventCallback2 object to be used to send debug events that occur during expression 
        /// evaluation. </param>
        /// <param name="fWatch"> If non-zero (TRUE), allows expression evaluation on the thread identified by dwTid; otherwise, zero 
        /// (FALSE) disallows expression evaluation on that thread.</param>
        /// <returns> VSConstants.S_OK. </returns>
        public int WatchForExpressionEvaluationOnThread(IDebugProgram2 pOriginatingProgram, uint dwTid, uint dwEvalFlags, IDebugEventCallback2 pExprCallback, int fWatch)
        {
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Watches for execution (or stops watching for execution) to occur on the given thread. WatchForThreadStep is used to 
        /// cooperate between two different engines debugging the same process. The VSNDK debug engine doesn't cooperate with other 
        /// engines, so it has nothing to do here. (http://msdn.microsoft.com/en-us/library/bb161406.aspx)
        /// </summary>
        /// <param name="pOriginatingProgram"> An IDebugProgram2 object representing the program being stepped. </param>
        /// <param name="dwTid"> Specifies the identifier of the thread to watch. </param>
        /// <param name="fWatch"> Non-zero (TRUE) means start watching for execution on the thread identified by dwTid; otherwise, 
        /// zero (FALSE) means stop watching for execution on dwTid. </param>
        /// <param name="dwFrame"> Specifies a frame index that controls the step type. </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int WatchForThreadStep(IDebugProgram2 pOriginatingProgram, uint dwTid, int fWatch, uint dwFrame)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region IDebugSymbolSettings100 members


        /// <summary>
        /// The SDM will call this method on the debug engine when it is created, to notify it of the user's
        /// symbol settings in Tools->Options->Debugging->Symbols. (http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.debugger.interop.idebugsymbolsettings100.setsymbolloadstate.aspx)
        /// </summary>
        /// <param name="bIsManual"> true if 'Automatically load symbols: Only for specified modules' is checked. </param>
        /// <param name="bLoadAdjacent"> true if 'Specify modules'->'Always load symbols next to the modules' is checked. </param>
        /// <param name="strIncludeList"> semicolon-delimited list of modules when automatically loading 'Only specified modules' </param>
        /// <param name="strExcludeList"> semicolon-delimited list of modules when automatically loading 'All modules, unless excluded' </param>
        /// <returns> VSConstants.S_OK. </returns>
        public int SetSymbolLoadState(int bIsManual, int bLoadAdjacent, string strIncludeList, string strExcludeList)
        {
            return VSConstants.S_OK;
        }
        #endregion

        #region Thread methods


        /// <summary>
        /// Verify if the stack frame information specified by "flags" were already evaluated.
        /// </summary>
        /// <param name="threadId"> Thread ID. </param>
        /// <param name="flags"> Specifies the information to verify about a stack frame object. </param>
        /// <returns> If successful, returns TRUE; otherwise, returns FALSE. </returns>
        public bool evaluatedTheseFlags(string threadId, enum_FRAMEINFO_FLAGS flags)
        {
            if (this.thread != null)
            {
                int currentFlags = Convert.ToInt32(flags);
                int i = 0;
                // looking for the "threadId" thread.
                for (; i < this.thread.Length; i++)
                {
                    if (this.thread[i]._id == threadId)
                        break;
                }
                if (i < this.thread.Length)
                {
                    if (this.thread[i]._alreadyEvaluated == currentFlags)
                    {
                        // This stack frame information were already evaluated.
                        return true;
                    }
                    else
                    {
                        // This stack frame information were not evaluated before. Modifying the _alreadyEvaluated flag.
                        this.thread[i]._alreadyEvaluated = currentFlags;
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Clean the "flags" data (_alreadyEvaluated) that are stored in each thread without resetting the stack frames. This operation
        /// must be done whenever the stack frames for a given thread have to be evaluated, like when the user changes the current frame
        /// or when he wants to investigate different threads in break mode, for example.
        /// </summary>
        public void cleanEvaluatedThreads()
        {
            if (this.thread != null)
            {
                for (int i = 0; i < this.thread.Length; i++)
                {
                    this.thread[i]._alreadyEvaluated = 0;
                }
            }
        }


        /// <summary>
        /// Reset stack frames for each thread. This operation must be performed before changing the state to break mode, like when a 
        /// breakpoint is hit, for example.
        /// </summary>
        public void resetStackFrames()
        {
            foreach (AD7Thread t in m_threads)
            {
                if (t.__stackFrames != null)
                {
                    t.__stackFrames.Clear();
                    t.__stackFrames = null;
                }
            }
        }


        /// <summary>
        /// Update the list of threads. This operation must be performed whenever the list of threads changes (a thread exits or is
        /// created) and before entering in break mode.
        /// </summary>
        public void UpdateListOfThreads()
        {
            foreach (AD7Thread t in this.m_threads)
            {
                // Send events to VS to destroy its current debugged threads.
                AD7ThreadDestroyEvent.Send(this, 0, t);
            }

            this.m_threads = null;
            // Get the current list of threads and store them into a temporary variable listThreads.
            this._currentThreadIndex = GetListOfThreads(out this.m_threads);

            if ((this._currentThreadIndex == -1) || (this.m_threads == null))
                return;

            // Send events to VS to add this list of debugged threads.
            foreach (AD7Thread t in this.m_threads)
            {
                m_engineCallback.OnThreadStart(t);
            }

            this._updateThreads = false;
        }


        /// <summary>
        /// Get the list of threads being debugged, also returning the index of the current thread (or -1 in case of error)
        /// </summary>
        /// <param name="threadObjects"> Returns the current list of threads that are being debugged. </param>
        /// <returns> Returns the index of the current thread in the list of threads (or -1 in case of error) </returns>
        public int GetListOfThreads(out AD7Thread[] threadObjects)
        {
            // Gets the parsed response for the GDB/MI command that ask for information about all threads.
            // (http://www.sourceware.org/gdb/onlinedocs/gdb/GDB_002fMI-Thread-Commands.html)
            string threadResponse = GDBParser.parseCommand(@"-thread-info", 22);
            string[] threadStrings = threadResponse.Split('#');
            cleanEvaluatedThreads();

            // Query the threads depth without inquiring GDB.
            int numThreads = threadStrings.Length - 1; // the last item in threadStrings is not a thread but the current thread ID.
            string currentThread = threadStrings[threadStrings.Length - 1];
            if (numThreads < 1)
            {
                threadObjects = null;
                return -1;
            }

            try
            {
                threadObjects = new AD7Thread[numThreads];
                int currentThreadIndex = -1;

                for (int i = 0; i < numThreads; i++)
                {
                    string[] threadInfo = threadStrings[i].Split(';');

                    // Setting the current thread index.
                    if (currentThread == threadInfo[0])
                    {
                        currentThreadIndex = i;
                    }

                    // Each threadInfo has one of these two formats:
                    // with 5 elements: ID; State; targetID; details (not used); name -> this format is normally used for external code.
                    // with 7 elements: ID; State; targetID; details (not used); full short-path filename; line; name
                    if (threadInfo.Length > 5)
                        threadObjects[i] = new AD7Thread(this, threadInfo[0], threadInfo[2], threadInfo[1], "Normal", threadInfo[6], threadInfo[4], threadInfo[5]);
                    else
                        threadObjects[i] = new AD7Thread(this, threadInfo[0], threadInfo[2], threadInfo[1], "Normal", threadInfo[4], "", "");
                }
                return currentThreadIndex;
            }
            catch (ComponentException e)
            {
                threadObjects = null;
                return -1;
            }
            catch (Exception e)
            {
                threadObjects = null;
                return -1;
            }
        }


        /// <summary>
        /// Returns the current thread.
        /// </summary>
        /// <returns> If successful, returns the current thread; otherwise, returns NULL. </returns>
        public AD7Thread currentThread()
        {
            if (_currentThreadIndex != -1)
                return m_threads[_currentThreadIndex];
            return null;
        }


        /// <summary>
        /// Returns the thread data structure with a given ID.
        /// </summary>
        /// <param name="id"> The ID of the thread to search for. </param>
        /// <returns> If successful, returns the selected thread; otherwise, returns NULL. </returns>
        public AD7Thread selectThread(string id)
        {
            for (int i = 0; i < this.m_threads.Length; i++)
            {
                if (this.m_threads[i]._id == id)
                    return (m_threads[i]);
            }
            return null;
        }


        /// <summary>
        /// Set a given thread as the current one.
        /// </summary>
        /// <param name="id"> The ID of the thread. </param>
        public void setAsCurrentThread(string id)
        {
            for (int i = 0; i < this.m_threads.Length; i++)
            {
                if (this.m_threads[i]._id == id)
                {
                    this._currentThreadIndex = i;
                    break;
                }
            }
        }


        /// <summary>
        /// Add threads for a program when lauching it and the debugger (IDebugEngineLaunch2.LaunchSuspended).
        /// </summary>
        /// <returns> VSConstants.S_OK. </returns>
        public int AddThreadsToProgram()
        {
            m_threads = null;
            _currentThreadIndex = GetListOfThreads(out m_threads);
            if (_currentThreadIndex != -1)
            {
                foreach (AD7Thread t in m_threads)
                {
                    m_engineCallback.OnThreadStart(t);
                }
            }
            else
                    m_engineCallback.OnThreadStart(null);
            return VSConstants.S_OK;
        }

        #endregion

        #region Deprecated interface methods
        // These methods are not called by the Visual Studio debugger, so they don't need to be implemented


        /// <summary>
        /// Retrieves a list of all programs being debugged by a debug engine (DE). Not implemented.
        /// (http://msdn.microsoft.com/en-us/library/bb146175.aspx)
        /// </summary>
        /// <param name="pCallback"> Returns an object that contains a list of all programs being debugged by a DE. </param>
        /// <returns> VSConstants.E_NOTIMPL. </returns>
        int IDebugEngine2.EnumPrograms(out IEnumDebugPrograms2 programs)
        {
            Debug.Fail("This function is not called by the debugger");

            programs = null;
            return VSConstants.E_NOTIMPL;
        }


        /// <summary>
        /// Attaches to the program. Not implemented. (http://msdn.microsoft.com/en-us/library/bb161973.aspx)
        /// </summary>
        /// <param name="programs"> An IDebugEventCallback2 object to be used for debug event notification. </param>
        /// <returns> VSConstants.E_NOTIMPL. </returns>
        public int Attach(IDebugEventCallback2 pCallback)
        {
            Debug.Fail("This function is not called by the debugger");

            return VSConstants.E_NOTIMPL;
        }


        /// <summary>
        /// Get the process that this program is running in. Not implemented. (http://msdn.microsoft.com/en-us/library/bb145293.aspx)
        /// </summary>
        /// <param name="process"> Returns the IDebugProcess2 interface that represents the process. </param>
        /// <returns> VSConstants.E_NOTIMPL. </returns>
        public int GetProcess(out IDebugProcess2 process)
        {
            Debug.Fail("This function is not called by the debugger");

            process = null;
            return VSConstants.E_NOTIMPL;
        }


        /// <summary> TODO: Verify if this method is called or not by VS.
        /// Continues running this program from a stopped state. Any previous execution state (such as a step) is cleared, and the 
        /// program starts executing again. (http://msdn.microsoft.com/en-us/library/bb162315.aspx)
        /// </summary>
        /// <returns> VSConstants.S_OK. </returns>
        public int Execute()
        {
            m_eventDispatcher.continueExecution();
            return VSConstants.S_OK;
        }

        #endregion
    }
}

