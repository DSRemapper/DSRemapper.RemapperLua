using DSRemapper.Core;
using DSRemapper.DSRMath;
using DSRemapper.SixAxis;
using DSRemapper.Types;
using MoonSharp.Interpreter;

using MoonSharp.Interpreter.Serialization.Json;

namespace DSRemapper.RemapperLua
{
    /// <summary>
    /// Utils class for the lua remap profiles.
    /// Provides a set of useful functions to remap a controller (for advanced users or not so advanced too).
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Min controller axis value
        /// </summary>
        public const float MinAxis = -1;
        /// <summary>
        /// Zero value of controller axis
        /// </summary>
        public const float CenterAxis = 0;
        /// <summary>
        /// Max controller axis value
        /// </summary>
        public const float MaxAxis = 1;
        /// <summary>
        /// Min controller trigger axis value
        /// </summary>
        public const float MinTrigger = 0;
        /// <summary>
        /// Max controller trigger axis value
        /// </summary>
        public const float MaxTrigger = 1;
        /// <summary>
        /// Creates a default DSOutputReport object to set physical device feedback properties.
        /// </summary>
        /// <returns>A default DSOutputReport object</returns>
        public static DefaultDSROutputReport CreateOutputReport() => new();
        /// <summary>
        /// Creates a simple signal filter to reduce measurement noise from the raw IMU data.
        /// </summary>
        /// <returns>An inizialized simple signal filter</returns>
        public static SimpleSignalFilter CreateSSF() => new();
        /// <summary>
        /// Creates an exponetial moving averange.
        /// This allow to get the average from a series of numbers with a minimum memory usage.
        /// </summary>
        /// <returns>An initialized exponetial moving averange</returns>
        public static ExpMovingAverage CreateAverange() => new();
        /// <summary>
        /// Creates an exponetial moving averange for a 3D vector (useful to process IMU data)
        /// This allow to get the average from a series of 3D vectors with a minimum memory usage.
        /// </summary>
        /// <returns>An initialized exponetial moving averange for 3D vector</returns>
        public static ExpMovingAverageVector3 CreateAverangeVec3() => new();
        /// <summary>
        /// Creates an two dimensional vector
        /// </summary>
        /// <param name="val">X and Y value for the vector</param>
        /// <returns>An initialized 2D vector</returns>
        public static DSRVector2 CreateVector2(float val = 0) => new(val);
        /// <summary>
        /// Creates an two dimensional vector
        /// </summary>
        /// <param name="x">X value of the vector</param>
        /// <param name="y">Y value of the vector</param>
        /// <returns>An initialized 2D vector</returns>
        public static DSRVector2 CreateVector2(float x,float y) => new(x,y);
        /// <summary>
        /// Creates an three dimensional vector
        /// </summary>
        /// <param name="val">X, Y and Z value for the vector</param>
        /// <returns>An initialized 3D vector</returns>
        public static DSRVector3 CreateVector3(float val = 0) => new(val);
        /// <summary>
        /// Creates an three dimensional vector
        /// </summary>
        /// <param name="x">X value of the vector</param>
        /// <param name="y">Y value of the vector</param>
        /// <param name="z">Z value of the vector</param>
        /// <returns>An initialized 3D vector</returns>
        public static DSRVector3 CreateVector3(float x, float y,float z) => new(x,y,z);
        /// <summary>
        /// Creates a identity quaternion (X: 0, Y: 0, Z: 0, W: 1)
        /// </summary>
        /// <returns>A identity quaternion</returns>
        public static DSRQuaternion CreateQuaternion() => new();
        /// <summary>
        /// Basic deadzone function to keep axis centered until a threshold
        /// </summary>
        /// <param name="value">Input axis value</param>
        /// <param name="deadzone">Axis threshold value (it's applied to positive and negative axis values)</param>
        /// <returns>Output axis value with deadzone applied</returns>
        public static float Deadzone(this float value, float deadzone)
        {
            float valueAbs = value.Abs();
            return valueAbs < deadzone ? 0 : (valueAbs.Remap(deadzone, 1, 0, 1) * value.Sign());
        }
        /// <summary>
        /// A nonlinear axis function to convert lineal axis input into an exponential axis output.
        /// Useful to get precision for low axis values, without losing its range.
        /// With this function a mayor movement at the start of the axis is translated into a minor movement of the output axis, and a minor movement at the end of the axis is translated into a mayor movement of the output axis.
        /// </summary>
        /// <param name="value">Input axis value</param>
        /// <param name="noLinear">Nonlinear factor (0 = linear and 1 = cubic exponential)</param>
        /// <returns>Output axis with non-linearization applied</returns>
        public static float NoLinAxis(this float value, float noLinear)
        {
            noLinear = noLinear.Clamp(0,1);
            return noLinear*MathF.Pow(value,3)+(1-noLinear)*value;
        }
        /// <summary>
        /// Linearizes a sine function, useful for converting normalized vectors components of SixAxis (gravity-vector components) to a linear output.
        /// </summary>
        /// <param name="value">Vector component input</param>
        /// <returns>Linearized vector component</returns>
        public static float LinVecComp(this float value) => (2f / MathF.PI) * MathF.Asin(value);
        /// <summary>
        /// Absolute function
        /// </summary>
        /// <param name="value">Input number</param>
        /// <returns>Positive number of the same magnitude of the input number</returns>
        public static float Abs(this float value) => Math.Abs(value);
        /// <summary>
        /// Converts one range to another. Converts outside of the input range too.
        /// </summary>
        /// <param name="value">Input value</param>
        /// <param name="from1">Min input range reference</param>
        /// <param name="to1">Max input range reference</param>
        /// <param name="from2">Min output range reference</param>
        /// <param name="to2">Max output range reference</param>
        /// <returns>Equivalent of the input value in the output range</returns>
        public static float Remap(this float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
        /// <summary>
        /// Constrains a value to the given limits
        /// </summary>
        /// <param name="value">Input value</param>
        /// <param name="min">Min value</param>
        /// <param name="max">Max value</param>
        /// <returns>A value between min and max values</returns>
        public static float Clamp(this float value, float min, float max)
        {
            return Math.Clamp(value, min, max);
        }
        /// <summary>
        /// Constrains a value to the default axis limits [-1, 1]
        /// </summary>
        /// <param name="value">Input value</param>
        /// <returns>A value between -1 and 1</returns>
        public static float ClampAxis(this float value) => value.Clamp(MinAxis, MaxAxis);
        /// <summary>
        /// Constrains a value to the default trigger limits [0, 1]
        /// </summary>
        /// <param name="value">Input value</param>
        /// <returns>A value between 0 and 1</returns>
        public static float ClampTrigger(this float value) => value.Clamp(MinTrigger, MaxTrigger);
        /// <summary>
        /// Gets the sign of a number.
        /// </summary>
        /// <param name="value">Input value</param>
        /// <returns>-1 for negative numbers and 1 for positive numbers (zero included)</returns>
        public static float Sign(this float value)
        {
            return Math.Sign(value);
        }
        /// <summary>
        /// A power function which multiplies the sign of the input to the output (to get even powers with negative and positive sign).
        /// </summary>
        /// <param name="value">Input value</param>
        /// <param name="pow">Power value</param>
        /// <returns>The input value raised to the power value and multiplied by its sign</returns>
        public static float SignPow(this float value, float pow)
        {
            return (float)Math.Pow(value, pow) * Math.Sign(value);
        }
        /// <summary>
        /// Counter part of the NoLinAxis axis function.
        /// </summary>
        /// <param name="value">Input axis value</param>
        /// <param name="bend">Cubic factor (0 = cubic exponential and 1 = linear)</param>
        /// <returns>Output axis with non-linearization applied</returns>
        public static float Bend(this float value, float bend)
        {
            return (1 - bend) * (float)Math.Pow(value, 3) + bend * value;
        }
        /// <summary>
        /// Saves a lua table to a file
        /// </summary>
        /// <param name="path">A path relative to the DSRemapper profile config folder</param>
        /// <param name="table">Lua table to be saved in the file</param>
        public static void SaveTable(string path, Table table)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string json = JsonTableConverter.TableToJson(table);
            if (Path.IsPathRooted(path))
                File.WriteAllText(path, json);
            else
                File.WriteAllText(Path.Combine(DSRPaths.ConfigPath,path), json);
        }
        /// <summary>
        /// Loads a lua table from a file
        /// </summary>
        /// <param name="path">A path relative to the DSRemapper profile config folder</param>
        /// <returns>Lua table saved in the file</returns>
        public static Table LoadTable(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string json;
            if (Path.IsPathFullyQualified(path))
                json = File.ReadAllText(path);
            else
                json = File.ReadAllText(Path.Combine(DSRPaths.ConfigPath, path));

            return JsonTableConverter.JsonToTable(json);
        }
    }
}
