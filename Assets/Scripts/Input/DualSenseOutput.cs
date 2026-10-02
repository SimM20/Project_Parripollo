using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

/// <summary>Cómo le pide la vibración al DualSense (ver <see cref="DualSenseOutput"/>).</summary>
public enum DualSenseRumbleMode
{
    /// <summary>Emulación mejorada de motores: firmware 2.24 o más nuevo (cualquier control que se actualizó alguna vez). Más fuerte y más nítida.</summary>
    Improved,
    /// <summary>Emulación clásica: la única que entienden los firmwares de fábrica (2020). Más débil.</summary>
    Classic,
}

/// <summary>
/// Reporte de salida del DualSense armado a mano, en lugar del <c>SetMotorSpeeds</c> del Input
/// System, que con este control tiene dos problemas:
///  • Solo anda por USB: por Bluetooth el paquete no calcula el CRC32 del reporte y el control
///    lo descarta (está marcado como FIXME en <c>DualShockGamepadHID.cs</c>).
///  • Cada vibración reescribe la barra de luz, y si nadie le fijó un color la apaga.
///
/// Acá se manda el reporte 0x02 (USB) o 0x31 (Bluetooth, con secuencia y CRC), con la emulación
/// de motores que tiene el control en sus actuadores hápticos, y la barra de luz se toca solo si
/// se pide un color. Formato del reporte: el mismo que usan el driver <c>hid-playstation</c> de
/// Linux y SDL.
///
/// No es la háptica HD de los juegos de PS5: esa se maneja como un canal de audio del control
/// (solo por USB, con un plugin nativo que el Input System no tiene). Lo que sí se aprovecha es que
/// los actuadores del DualSense responden mucho más rápido que un motor de Xbox: los pulsos cortos
/// se sienten como golpecitos y no como zumbidos.
/// </summary>
public static class DualSenseOutput
{
    // ── Reporte común (47 bytes, igual por USB y Bluetooth) ──
    private const int PayloadSize = 47;
    private const int ValidFlag0 = 0;
    private const int ValidFlag1 = 1;
    private const int MotorRight = 2;      // actuador derecho: alta frecuencia (golpecitos)
    private const int MotorLeft = 3;       // actuador izquierdo: baja frecuencia (peso)
    private const int ValidFlag2 = 38;
    private const int LightbarSetup = 41;
    private const int LightbarRed = 44;

    private const byte Flag0CompatibleVibration = 0x01;
    private const byte Flag0HapticsSelect = 0x02;          // motores emulados en vez de audio háptico
    private const byte Flag1LightbarControl = 0x04;
    private const byte Flag2LightbarSetupControl = 0x02;
    private const byte Flag2CompatibleVibration2 = 0x04;
    private const byte LightbarSetupLightOut = 0x02;       // apaga la animación azul de arranque: deja manejar el color

    private const byte UsbReportId = 0x02;
    private const byte BluetoothReportId = 0x31;
    private const byte BluetoothTag = 0x10;
    private const int BluetoothReportSize = 78;            // id + secuencia + tag + 47 + relleno + CRC32
    private const int BluetoothCrcOffset = 74;
    private const byte BluetoothCrcSeed = 0xA2;            // byte de "salida HID" que entra en el CRC y no se manda

    private class PadState
    {
        public bool bluetooth;
        public int reportSize;
        public byte sequence;
        public bool lightbarReady;
        public bool failed;
    }

    private static readonly Dictionary<int, PadState> pads = new Dictionary<int, PadState>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pads.Clear();

    /// <summary>
    /// Fija los dos actuadores (0..1) y, si <paramref name="lightbar"/> tiene valor, el color de la
    /// barra. Devuelve false si el control rechazó el reporte (se avisa una sola vez por control).
    /// </summary>
    public static bool Send(DualSenseGamepadHID pad, float low, float high, Color? lightbar, DualSenseRumbleMode mode)
    {
        if (pad == null || !pad.added) return false;

        PadState state = GetState(pad);

        var command = new OutputCommand
        {
            baseCommand = new InputDeviceCommand(OutputCommand.Type, InputDeviceCommand.BaseCommandSize + state.reportSize),
        };

        Span<byte> report = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref command, 1))
            .Slice(InputDeviceCommand.BaseCommandSize, state.reportSize);

        Span<byte> payload;
        if (state.bluetooth)
        {
            report[0] = BluetoothReportId;
            report[1] = (byte)(state.sequence << 4);
            report[2] = BluetoothTag;
            state.sequence = (byte)((state.sequence + 1) & 0x0F);
            payload = report.Slice(3, PayloadSize);
        }
        else
        {
            report[0] = UsbReportId;
            payload = report.Slice(1, PayloadSize);
        }

        payload[ValidFlag0] = Flag0HapticsSelect;
        if (mode == DualSenseRumbleMode.Classic)
            payload[ValidFlag0] |= Flag0CompatibleVibration;
        else
            payload[ValidFlag2] |= Flag2CompatibleVibration2;

        payload[MotorLeft] = ToByte(low);
        payload[MotorRight] = ToByte(high);

        if (lightbar.HasValue)
        {
            if (!state.lightbarReady)
            {
                payload[ValidFlag2] |= Flag2LightbarSetupControl;
                payload[LightbarSetup] = LightbarSetupLightOut;
                state.lightbarReady = true;
            }

            Color c = lightbar.Value;
            payload[ValidFlag1] |= Flag1LightbarControl;
            payload[LightbarRed] = ToByte(c.r);
            payload[LightbarRed + 1] = ToByte(c.g);
            payload[LightbarRed + 2] = ToByte(c.b);
        }

        if (state.bluetooth)
        {
            uint crc = Crc32(BluetoothCrcSeed, report.Slice(0, BluetoothCrcOffset));
            report[BluetoothCrcOffset] = (byte)crc;
            report[BluetoothCrcOffset + 1] = (byte)(crc >> 8);
            report[BluetoothCrcOffset + 2] = (byte)(crc >> 16);
            report[BluetoothCrcOffset + 3] = (byte)(crc >> 24);
        }

        bool ok = pad.ExecuteCommand(ref command) >= 0;
        if (!ok && !state.failed)
        {
            state.failed = true;
            Debug.LogWarning($"[DualSenseOutput] '{pad.displayName}' rechazó el reporte de vibración " +
                             $"({(state.bluetooth ? "Bluetooth" : "USB")}, {state.reportSize} bytes).");
        }
        return ok;
    }

    private static PadState GetState(DualSenseGamepadHID pad)
    {
        if (pads.TryGetValue(pad.deviceId, out PadState state))
            return state;

        // El tamaño del reporte de salida lo dice el descriptor HID: 48 por USB; por Bluetooth el
        // descriptor declara reportes de hasta 547 bytes y Windows pide el buffer de ese largo
        // (el reporte 0x31 va al principio y el resto en cero).
        int size = 0;
        try
        {
            size = HID.HIDDeviceDescriptor.FromJson(pad.description.capabilities).outputReportSize;
        }
        catch (Exception) { }

        state = new PadState { bluetooth = size >= BluetoothReportSize };
        int minimum = state.bluetooth ? BluetoothReportSize : PayloadSize + 1;
        state.reportSize = Mathf.Clamp(Mathf.Max(size, minimum), minimum, OutputCommand.MaxReportSize);

        pads[pad.deviceId] = state;
        return state;
    }

    private static byte ToByte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

    // ── CRC32 (polinomio 0xEDB88320, el estándar) ──

    private static uint[] crcTable;

    private static uint Crc32(byte seed, ReadOnlySpan<byte> data)
    {
        if (crcTable == null)
        {
            crcTable = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                crcTable[i] = c;
            }
        }

        uint crc = 0xFFFFFFFFu;
        crc = crcTable[(crc ^ seed) & 0xFF] ^ (crc >> 8);
        for (int i = 0; i < data.Length; i++)
            crc = crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    /// <summary>
    /// Comando 'HIDO' (escribir un reporte de salida HID). El tamaño fijo reserva lugar para el
    /// reporte más largo; <c>baseCommand.sizeInBytes</c> dice cuánto se manda de verdad. Se llena
    /// por <see cref="MemoryMarshal"/> para no necesitar código unsafe en el proyecto.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = InputDeviceCommand.BaseCommandSize + MaxReportSize)]
    private struct OutputCommand : IInputDeviceCommandInfo
    {
        public const int MaxReportSize = 640;
        public static FourCC Type => new FourCC('H', 'I', 'D', 'O');
        public FourCC typeStatic => Type;

        [FieldOffset(0)] public InputDeviceCommand baseCommand;
    }
}
