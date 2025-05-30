// Contains parts from
// https://raw.githubusercontent.com/mamedev/mame/master/src/devices/bus/neogeo/prot_sma.cpp
// license:BSD-3-Clause
// copyright-holders:S. Smith,David Haywood,Fabio Priuli

using System;

public class ProtSMA
{
    public static void P1Decrypt(byte[] rom, byte[] map, int baseAddr, byte[] baseRemap, byte[] portRemap)
    {
        for (int i = 0; i < rom.Length / 2; i++)
        {
            byte hi = (byte)(
                (((rom[i * 2 + (map[0]  >> 3)] >> (map[0]  & 7)) & 1) << 7) |
                (((rom[i * 2 + (map[1]  >> 3)] >> (map[1]  & 7)) & 1) << 6) |
                (((rom[i * 2 + (map[2]  >> 3)] >> (map[2]  & 7)) & 1) << 5) |
                (((rom[i * 2 + (map[3]  >> 3)] >> (map[3]  & 7)) & 1) << 4) |
                (((rom[i * 2 + (map[4]  >> 3)] >> (map[4]  & 7)) & 1) << 3) |
                (((rom[i * 2 + (map[5]  >> 3)] >> (map[5]  & 7)) & 1) << 2) |
                (((rom[i * 2 + (map[6]  >> 3)] >> (map[6]  & 7)) & 1) << 1) |
                (((rom[i * 2 + (map[7]  >> 3)] >> (map[7]  & 7)) & 1) << 0));
            byte lo = (byte)(
                (((rom[i * 2 + (map[8]  >> 3)] >> (map[8]  & 7)) & 1) << 7) |
                (((rom[i * 2 + (map[9]  >> 3)] >> (map[9]  & 7)) & 1) << 6) |
                (((rom[i * 2 + (map[10] >> 3)] >> (map[10] & 7)) & 1) << 5) |
                (((rom[i * 2 + (map[11] >> 3)] >> (map[11] & 7)) & 1) << 4) |
                (((rom[i * 2 + (map[12] >> 3)] >> (map[12] & 7)) & 1) << 3) |
                (((rom[i * 2 + (map[13] >> 3)] >> (map[13] & 7)) & 1) << 2) |
                (((rom[i * 2 + (map[14] >> 3)] >> (map[14] & 7)) & 1) << 1) |
                (((rom[i * 2 + (map[15] >> 3)] >> (map[15] & 7)) & 1) << 0));
            rom[i * 2 + 1] = hi;
            rom[i * 2 + 0] = lo;
        }

        byte[] baseData = new byte[0xc0000];
        for (int i = 0; i < 0xc0000; i += 2)
        {
            int remappedAddr = baseAddr;
            for (int j = 0; j < 19; j++)
            {
                remappedAddr += ((i >> baseRemap[j]) & 2) << (18 - j);
            }
            baseData[i] = rom[remappedAddr];
            baseData[i + 1] = rom[remappedAddr + 1];
        }
        Buffer.BlockCopy(baseData, 0, rom, 0x700000, 0xc0000);
        int blockSize = 2 << portRemap.Length;
        for (int b = 0; b < 0x700000; b += blockSize)
        {
            for (int i = 0; i < blockSize; i += 2)
            {
                int remappedAddr = b;
                for (int j = 0; j < portRemap.Length; j++)
                {
                    remappedAddr += ((i >> portRemap[j]) & 2) << (portRemap.Length - 1 - j);
                }
                baseData[i] = rom[remappedAddr];
                baseData[i + 1] = rom[remappedAddr + 1];
            }
            Buffer.BlockCopy(baseData, 0, rom, b, blockSize);
        }
    }
}
