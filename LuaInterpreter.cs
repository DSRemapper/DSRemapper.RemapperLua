using DSRemapper.Core;
using DSRemapper.DSRMath;
using DSRemapper.SixAxis;
using DSRemapper.Types;
using DSRemapper.DSROutput;
using MoonSharp.Interpreter;
using System.Diagnostics;
using System.Numerics;
using DSRemapper.MouseKeyboardOutput;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Reflection;

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
            UserData.RegisterType<IDSRFeedback>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRTouch>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRTouch[]>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRLight>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRPov>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DSRPov[]>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DefaultDSROutputReport>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<DefaultDSRFFBData>(InteropAccessMode.BackgroundOptimized);

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
            UserData.RegisterType<byte[]>(InteropAccessMode.BackgroundOptimized);
            UserData.RegisterType<float[]>(InteropAccessMode.BackgroundOptimized);
        }

        private Script script = new();
        private Closure? luaRemap = null;
        /// <inheritdoc/>
        public event DeviceConsoleEventArgs? OnDeviceConsole;
        private string lastMessage = "";

        private readonly DSROutput.DSROutput emuControllers = new();

        DSRLogger logger;
        /// <summary>
        /// LuaInterpreter class constructor
        /// </summary>
        public LuaInterpreter(DSRLogger logger)
        {
            this.logger = logger;
        }
        /// <inheritdoc/>
        public void SetScript(string file, Dictionary<string, Delegate> customMethods)
        {
            try
            {
                emuControllers.DisconnectAll();
                script = new Script();

                Table customFuncs = new(script);
                foreach(var method in customMethods)
                {
                    customFuncs[method.Value.Method.Name] = method.Value;
                }
                script.Globals["CustomFuncs"] = customFuncs;

                script.Globals["CreatePov"] = ()=>new DSRPov();
                script.Globals["CreateFFB"] = ()=>new DefaultDSRFFBData();

                script.Globals["ConsoleLog"] = (Action<string>)ConsoleLog;
                script.Globals["Emulated"] = emuControllers;
                script.Globals["Utils"] = typeof(Utils);
                script.Globals["inputFB"] = Utils.CreateOutputReport();
                script.Globals["deltaTime"] = 0.0;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    script.Globals["MKOut"] = new MKOutput();
                    script.Globals["Keys"] = new VirtualKeyShort();
                    script.Globals["Scans"] = new ScanCodeShort();
                    script.Globals["MButs"] = new MouseButton();
                }

                script.DoFile(file);

                Closure? remapFunction = (Closure)script.Globals["Remap"];
                luaRemap = remapFunction;
                if (remapFunction == null)
                {
                    string msg = "No Remap function on the script";
                    logger.LogWarning(msg);
                    OnDeviceConsole?.Invoke(this, msg, LogLevel.Warning);
                }
            }
            catch (InterpreterException e)
            {
                luaRemap = null;
                string msg = e.DecoratedMessage;
                logger.LogError(msg);
                OnDeviceConsole?.Invoke(this, msg, LogLevel.Error);
            }
            catch (Exception e)
            {
                luaRemap = null;
                logger.LogError(e.Message);
            }
        }
        /// <inheritdoc/>
        public IDSROutputReport Remap(IDSRInputReport report, double deltaTime)
        {
            IDSROutputReport outReport = new DefaultDSROutputReport();
            try
            {
                script.Globals["deltaTime"] = deltaTime;
                if (luaRemap != null)
                    script.Call(luaRemap,report);
                IDSROutputReport? feedback = (IDSROutputReport?)script.Globals["inputFB"];
                if(feedback!=null)
                    outReport = feedback;
            }
            catch (InterpreterException e)
            {
                luaRemap = null;
                string msg = e.DecoratedMessage;
                logger.LogError(msg);
                OnDeviceConsole?.Invoke(this, msg, LogLevel.Error);
            }
            catch (Exception e)
            {
                if (lastMessage != e.Message)
                {
                    lastMessage = e.Message;
                    logger.LogError(e.Message);
                }
                luaRemap = null;
            }

            return outReport;
        }
        private void ConsoleLog(string text)
        {
            OnDeviceConsole?.Invoke(this,text,LogLevel.None);
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            emuControllers.DisconnectAll();
            emuControllers.Dispose();
            //GC.SuppressFinalize(this);
        }
    }
}