using DSRemapper.Core;
using DSRemapper.DSRMath;
using DSRemapper.SixAxis;
using DSRemapper.Types;
using DSRemapper.DSROutput;
using MoonSharp.Interpreter;
using System.Diagnostics;
using System.Numerics;
using DSRemapper.MouseKeyboardOutput;
using FireLibs.Logging;

namespace DSRemapper.RemapperLua
{
    /// <summary>
    /// Remapper plugin based on lua scripts for remapping controllers
    /// </summary>
    [Remapper(["lua","slua"])]
    public class LuaInterpreter : IDSRemapper
    {
        static LuaInterpreter()
        {
            UserData.RegisterType<IDSRInputReport>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<IDSROutputReport>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRTouch>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRTouch[]>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRLight>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRPov>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRPov[]>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DefaultDSROutputReport>(InteropAccessMode.BackgroundOptimized);

            UserData.RegisterType(typeof(Utils), InteropAccessMode.BackgroundOptimized);
            UserData.RegisterExtensionType(typeof(Utils), InteropAccessMode.BackgroundOptimized);

            UserData.RegisterType<IDSROutputController>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSROutput.DSROutput>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<MKOutput>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<VirtualKeyShort>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<ScanCodeShort>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<MouseButton>(InteropAccessMode.BackgroundOptimized);

            UserData.RegisterType<Vector2>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<Vector3>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<Quaternion>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRVector2>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRVector3>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRQuaternion>(InteropAccessMode.BackgroundOptimized);

            UserData.RegisterType<SimpleSignalFilter>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<ExpMovingAverage>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<ExpMovingAverageVector3>(InteropAccessMode.BackgroundOptimized);

            UserData.RegisterType<bool[]>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<float[]>(InteropAccessMode.BackgroundOptimized);
        }

        private Script script = new();
        private Closure? luaRemap = null;
        /// <inheritdoc/>
        public event RemapperEventArgs? OnLog;
        private string lastMessage = "";

        private readonly DSROutput.DSROutput emuControllers = new();

        private readonly Stopwatch sw = new();
        /// <summary>
        /// LuaInterpreter class constructor
        /// </summary>
        public LuaInterpreter()
        {

        }
        /// <inheritdoc/>
        public void SetScript(string file)
        {
            try
            {
                emuControllers.DisconnectAll();
                script=new Script();

                script.Globals["CreatePov"] = ()=>new DSRPov();

                script.Globals["ConsoleLog"] = (Action<string>)ConsoleLog;
                script.Globals["Emulated"] = emuControllers;
                script.Globals["Utils"] = typeof(Utils);
                script.Globals["inputFB"] = Utils.CreateOutputReport();
                script.Globals["deltaTime"] = 0.0;

                script.Globals["MKOut"] = new MKOutput();
                script.Globals["Keys"] = new VirtualKeyShort();
                script.Globals["Scans"] = new ScanCodeShort();
                script.Globals["MButs"] = new MouseButton();

                script.DoFile(file);

                Closure? remapFunction = (Closure)script.Globals["Remap"];
                luaRemap = remapFunction;
                if (remapFunction == null)
                    OnLog?.Invoke(this, LogLevel.Warning, false, "No Remap function on the script");
            }
            catch (InterpreterException e)
            {
                luaRemap = null;
                OnLog?.Invoke(this, LogLevel.Error, false, e.DecoratedMessage);
            }
            catch (Exception e)
            {
                luaRemap = null;
                OnLog?.Invoke(this, LogLevel.Error, false, e.Message);
            }
        }
        /// <inheritdoc/>
        public IDSROutputReport Remap(IDSRInputReport report)
        {
            IDSROutputReport outReport = new DefaultDSROutputReport();
            try
            {
                script.Globals["deltaTime"] = sw.Elapsed.TotalSeconds;
                sw.Restart();
                if (luaRemap != null)
                    script.Call(luaRemap,report);
                IDSROutputReport? feedback = (IDSROutputReport?)script.Globals["inputFB"];
                if(feedback!=null)
                    outReport = feedback;
            }
            catch (InterpreterException e)
            {
                luaRemap = null;
                OnLog?.Invoke(this,LogLevel.Error,false, e.DecoratedMessage);
            }
            catch (Exception e)
            {
                if (lastMessage != e.Message)
                {
                    lastMessage = e.Message;
                    OnLog?.Invoke(this,LogLevel.Error,false, e.Message);
                }
                luaRemap = null;
            }

            return outReport;
        }
        private void ConsoleLog(string text)
        {
            OnLog?.Invoke(this,LogLevel.None,true,text);
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            emuControllers.DisconnectAll();
            emuControllers.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}