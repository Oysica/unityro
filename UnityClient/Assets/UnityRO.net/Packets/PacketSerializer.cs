using ROIO.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/**
 * Note:
 * When working with packets, sometimes the server will send multiple
 * packets all at once. In those cases, we'll receive them all together.
 * That's why we iterate in a while once we receive bytes to read
 */
public class PacketSerializer {

    public struct PacketInfo {
        public int Size;
        public Type Type;
    }

    public MemoryStream Memory { get; set; }
    public int BytesToSkip { get; set; }

    public static Dictionary<ushort, PacketInfo> RegisteredPackets;

    // Unhandled packets already reported, so each is logged once
    private static readonly HashSet<ushort> SkippedPackets = new HashSet<ushort>();

    private IPacketHandler PacketHandler;

    static PacketSerializer() {
        RegisteredPackets = new Dictionary<ushort, PacketInfo>();

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(type => type.GetInterface("InPacket") != null)) {
            object[] attributes = type.GetCustomAttributes(typeof(PacketHandlerAttribute), true); // get the attributes of the packet.
            if (attributes.Length == 0)
                return;
            PacketHandlerAttribute ma = (PacketHandlerAttribute) attributes[0];
            RegisteredPackets.Add(ma.MethodId, new PacketInfo { Size = ma.Size, Type = type });
        }
    }

    public PacketSerializer(IPacketHandler packetHandler) {
        PacketHandler = packetHandler;
        Memory = new MemoryStream();
    }

    public void Reset() {
        Memory = new MemoryStream();
    }

    public void EnqueueBytes(byte[] data, int size) {
        int pos = (int) Memory.Position;
        Memory.Position = Memory.Length;
        Memory.Write(data, 0, size);
        Memory.Position = pos;

        ReadPacket();
    }

    private void ReadPacket() {
        if (BytesToSkip > 0) {
            int skipped = (int) Math.Min(BytesToSkip, Memory.Length - Memory.Position);
            Memory.Position += skipped;
            BytesToSkip -= skipped;
        }

        while (Memory.Length - Memory.Position >= 2) {
            // Commands are always the first two bytes
            // Followed by either the packet data in case the packet
            // has its size fixed, or the packet length
            long start = Memory.Position;
            var tmp = new byte[2];
            Memory.Read(tmp, 0, 2);
            ushort cmd = BitConverter.ToUInt16(tmp, 0);

            if (!RegisteredPackets.ContainsKey(cmd)) {
                if (KnownPacketSizes.Sizes.TryGetValue(cmd, out var skipSize)) {
                    // A real server packet this client doesn't handle yet: step over it and keep parsing the batch
                    bool variable = skipSize < 0;
                    if (variable) {
                        if (Memory.Length - Memory.Position < 2) {
                            Memory.Position = start;
                            break;
                        }
                        Memory.Read(tmp, 0, 2);
                        skipSize = BitConverter.ToUInt16(tmp, 0);
                    }

                    if (skipSize >= (variable ? 4 : 2)) {
                        if (Memory.Length - start < skipSize) {
                            Memory.Position = start;
                            break;
                        }
                        Memory.Position = start + skipSize;
                        if (SkippedPackets.Add(cmd)) {
                            Debug.Log($"Skipped unhandled packet {string.Format("0x{0:x4}", cmd)} ({(PacketHeader) cmd}, {skipSize}b)");
                        }
                        continue;
                    }
                }

                // We don't know the size of the packet, so nothing after it in this batch can be parsed either
                // The bytes just before the bad header show which registered packet had the wrong size
                long from = Math.Max(0, start - 16);
                var around = BitConverter.ToString(Memory.GetBuffer(), (int) from, (int) Math.Min(Memory.Length - from, start - from + 32));
                Debug.LogWarning($"Received Unknown Command: {string.Format("0x{0:x4}", cmd)}\nProbably: {(PacketHeader) cmd}\nBytes (from -{start - from}): {around}");
                DumpReceivedPacket(cmd, -1, Memory.Length - Memory.Position);
                Memory.Position = Memory.Length;
                break;
            } else {
                int size = RegisteredPackets[cmd].Size;
                bool isFixed = true;

                if (size <= 0) {
                    isFixed = false;

                    if (Memory.Length - Memory.Position < 2) {
                        // Length field not received yet
                        Memory.Position = start;
                        break;
                    }
                    Memory.Read(tmp, 0, 2);
                    size = BitConverter.ToUInt16(tmp, 0);

                    if (size < 4) {
                        Debug.LogWarning($"Received {(PacketHeader) cmd} with invalid length {size}");
                        Memory.Position = Memory.Length;
                        break;
                    }
                }

                // A packet split across TCP reads: wait for the rest instead of parsing a truncated one
                if (Memory.Length - start < size) {
                    Memory.Position = start;
                    break;
                }

                // Read skipping command and length
                byte[] data = new byte[size];
                Memory.Read(data, 0, size - (isFixed ? 2 : 4));

                ConstructorInfo ci = RegisteredPackets[cmd].Type.GetConstructor(new Type[] { });
                InPacket packet = (InPacket) ci.Invoke(null);
                using var br = new MemoryStreamReader(data);
                packet.Read(br, size - (isFixed ? 2 : 4));

                ThreadManager.ExecuteOnMainThread(() => {
                    PacketHandler.OnPacketReceived(packet);
                });

                PacketReceived?.Invoke(cmd, size, packet);
                DumpReceivedPacket(cmd, size, Memory.Length - Memory.Position);
            }
        }

        // Keep only the unread tail, positioned at its start so the next EnqueueBytes resumes from it
        MemoryStream ms = new MemoryStream();
        ms.Write(Memory.GetBuffer(), (int) Memory.Position, (int) (Memory.Length - Memory.Position));
        ms.Position = 0;
        Memory.Dispose();

        Memory = ms;
    }

    private static void DumpReceivedPacket(ushort cmd, int size, long remainingSize, InPacket packet = null) {
#if DUMP_RECEIVED_PACKET
        try {
            var log = $"{string.Format("0x{0:x3}", cmd)} \tReceived Size:{size} \tRegistered Size:{RegisteredPackets.Where(it => it.Key == cmd).FirstOrDefault().Value.Size} \tRemaining Size: {remainingSize} \t// {(PacketHeader) cmd}";
            Debug.Log(log);
        } catch (Exception e) {
            Debug.LogException(e);
        }
#endif
    }

    public event Action<ushort, int, InPacket> PacketReceived;
    public delegate void OnPacketReceived(ushort cmd, int size, InPacket packet);
}