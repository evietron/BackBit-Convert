
using System;
using System.Linq;
using System.Numerics;
using System.Text.Unicode;
using System.Xml.Linq;

namespace NeoDecode
{
    internal class Program
    {
        const string VERSION = "0.9.6a";

        enum CartType
        {
            GENERIC,
            CMC_42,
            CMC_50
        }

        enum CartID
        {
            JOCKEY   = 8,
            ZUPAPA   = 70,
            KOF98    = 242,
            KOF99    = 251,
            GANRYU   = 252,
            GAROU    = 253,
            S1945P   = 254,
            PREISLE2 = 255,
            MSLUG3   = 256,
            KOF2000  = 257,
            BANGBEAD = 259,
            NITD     = 260,
            SENGOKU3 = 261,
            KOF2001  = 262,
            MSLUG4   = 263,
            ROTD     = 264,
            KOF2002  = 265,
            MATRIM   = 266,
            PNYAA    = 267,
            MSLUG5   = 268,
            SVC      = 269,
            SAMSHO5  = 270,
            KOF2003  = 271,
            SAMSH5SP = 272,

            BiggerThanAnyNormalID = 10000, // so it won't conflict, these are temporary
            SBP, // Super Bubble Pop
            TEOT // The Eye of Typhoon
        }

        static readonly byte[] NEO_GEO_HEADER = { 0x45, 0x4E, 0x2D, 0x4F, 0x45, 0x47 };
        static readonly byte[] NEO_GEO_HEADER_ODD = { 0x45, 0x2D, 0x45 };
        static readonly byte[] NEO_GEO_HEADER_EVEN = { 0x4E, 0x4F, 0x47 };
        static readonly byte[] NEO_SD_HEADER = { 0x4E, 0x45, 0x4F, 0x01 };

        public static void CreateBinary(string name, byte[] data)
        {
            FileStream fs = File.Create(name);
            fs.Write(data);
            fs.Close();
        }

        public static void P1Swap(byte[] rom, byte[] chunks)
        {
            byte[] buf = new byte[rom.Length];
            Buffer.BlockCopy(rom, 0, buf, 0, rom.Length);
            for (int i = 0; i < chunks.Length; i++)
            {
                Buffer.BlockCopy(buf, chunks[i] * 0x10000, rom, i * 0x80000, 0x80000);
            }
        }

        static void ParseCart(string dir, int cartID, CartType cartType, int extraXor, string name)
        {
            string cartFilePrefix = (cartID < 10000) ? cartID.ToString() : name;
            while (cartFilePrefix.Length < 3)
            {
                cartFilePrefix = "0" + cartFilePrefix;
            }

            Console.WriteLine("Found: " + name);

            if (new[] {
                (int)CartID.KOF98,
                (int)CartID.KOF99,
                (int)CartID.GAROU,
                (int)CartID.MSLUG3,
                (int)CartID.KOF2000,
                (int)CartID.KOF2002,
                (int)CartID.MATRIM, 
                (int)CartID.MSLUG5, 
                (int)CartID.SVC, 
                (int)CartID.SAMSHO5, 
                (int)CartID.KOF2003, 
                (int)CartID.SAMSH5SP, 
                (int)CartID.SBP, 
                (int)CartID.TEOT }.Contains(cartID))
            {
                Console.Write("Decoding PROM");

                byte[] prom = new byte[0];
                for (int i = 1; i <= 4; i++)
                {
                    string pName = FindCartSuffix(dir, "p" + i);
                    if (File.Exists(pName))
                    {
                        FileInfo f = new FileInfo(pName);
                        byte[] prom2 = new BinaryReader(f.OpenRead()).ReadBytes((int)f.Length);
                        byte[] promn = new byte[prom.Length + prom2.Length];
                        Buffer.BlockCopy(prom,  0, promn, 0, prom.Length);
                        Buffer.BlockCopy(prom2, 0, promn, prom.Length, prom2.Length);
                        prom = promn;
                    }
                }

                if (prom.Skip(0x100).Take(6).SequenceEqual(NEO_GEO_HEADER) &&
                    new[] {
                        (int)CartID.KOF99, 
                        (int)CartID.GAROU, 
                        (int)CartID.MSLUG3, 
                        (int)CartID.KOF2000 }.Contains(cartID))
                {
                    Console.WriteLine(" (Not Encrypted)...");
                }
                else
                {
                    switch (cartID)
                    {
                        case (int)CartID.KOF98:
                            prom = ProtKOF98.Decrypt(prom);
                            break;
                        case (int)CartID.KOF99:
                            ProtSMA.P1Decrypt(prom,
                                new byte[] { 13, 7, 3, 0, 9, 4, 5, 6, 1, 12, 8, 14, 10, 11, 2, 15 },
                                0x600000,
                                new byte[] { 18, 11, 6, 14, 17, 16, 5, 8, 10, 12, 0, 4, 3, 2, 7, 9, 15, 13, 1 },
                                new byte[] { 6, 2, 4, 9, 8, 3, 1, 7, 0, 5 });
                            break;
                        case (int)CartID.GAROU:
                            if (prom[0] == 0xf5)
                            {
                                Console.Write(" (Alternate)");
                                ProtSMA.P1Decrypt(prom,
                                    new byte[] { 14, 5, 1, 11, 7, 4, 10, 15, 3, 12, 8, 13, 0, 2, 9, 6 },
                                    0x6f8000,
                                    new byte[] { 18, 5, 16, 11, 2, 6, 7, 17, 3, 12, 8, 14, 4, 0, 9, 1, 10, 15, 13 },
                                    new byte[] { 12, 8, 1, 7, 11, 3, 13, 10, 6, 9, 5, 4, 0, 2 });
                            }
                            else
                            {
                                ProtSMA.P1Decrypt(prom, 
                                    new byte[] { 13, 12, 14, 10, 8, 2, 3, 1, 5, 9, 11, 4, 15, 0, 6, 7 },
                                    0x610000,
                                    new byte[] { 18, 4, 5, 16, 14, 7, 9, 6, 13, 17, 15, 3, 1, 2, 12, 11, 8, 10, 0 },
                                    new byte[] { 9, 4, 8, 3, 13, 6, 2, 7, 0, 12, 1, 11, 10, 5 });
                            }
                            break;
                        case (int)CartID.MSLUG3:
                            if (prom[2] == 0xfb)
                            {
                                Console.Write(" (Alternate)");
                                ProtSMA.P1Decrypt(prom, 
                                    new byte[] { 2, 11, 12, 14, 9, 3, 1, 4, 13, 7, 6, 8, 10, 15, 0, 5 },
                                    0x4d0000,
                                    new byte[] { 18, 1, 16, 14, 7, 17, 5, 8, 4, 15, 6, 3, 2, 0, 13, 10, 12, 9, 11 },
                                    new byte[] { 12, 0, 11, 3, 4, 13, 6, 8, 14, 7, 5, 2, 10, 9, 1 }); // A
                            }
                            else
                            {
                                ProtSMA.P1Decrypt(prom, 
                                    new byte[] { 4, 11, 14, 3, 1, 13, 0, 7, 2, 8, 12, 15, 10, 9, 5, 6 },
                                    0x4d0000,
                                    new byte[] { 18, 15, 2, 1, 13, 3, 0, 9, 6, 16, 4, 11, 5, 7, 12, 17, 14, 10, 8 },
                                    new byte[] { 2, 11, 0, 14, 6, 4, 13, 8, 9, 3, 10, 7, 5, 12, 1 });
                            }
                            break;
                        case (int)CartID.KOF2000:
                            ProtSMA.P1Decrypt(prom,
                                new byte[] { 12, 8, 11, 3, 15, 14, 7, 0, 10, 13, 6, 5, 9, 2, 1, 4 },
                                0x63a000,
                                new byte[] { 18, 8, 4, 15, 13, 3, 14, 16, 2, 6, 17, 7, 12, 10, 0, 5, 11, 1, 9 },
                                new byte[] { 4, 1, 3, 8, 6, 2, 7, 0, 9, 5 });
                            break;
                        case (int)CartID.KOF2002:
                        case (int)CartID.MATRIM:
                            P1Swap(prom, new byte[] { 0x00, 0x08, 0x20, 0x38, 0x40, 0x28, 0x10, 0x48, 0x30, 0x18 });
                            break;
                        case (int)CartID.MSLUG5:
                            ProtPVC.MSlug5Decrypt(prom);
                            break;
                        case (int)CartID.SVC:
                            ProtPVC.SVCDecrypt(prom);
                            break;
                        case (int)CartID.SAMSHO5:
                            P1Swap(prom, new byte[] { 0x00, 0x08, 0x70, 0x68, 0x50, 0x18, 0x20, 0x48, 0x30, 0x78, 0x60, 0x28, 0x10, 0x58, 0x40, 0x38 });
                            break;
                        case (int)CartID.KOF2003:
                            if (prom[0] == 0xd2)
                            {
                                Console.Write(" (Alternate)");
                                prom = ProtPVC.KOF2003HDecrypt(prom);
                            }
                            else
                            {
                                prom = ProtPVC.KOF2003Decrypt(prom);
                            }
                            break;
                        case (int)CartID.SAMSH5SP:
                            P1Swap(prom, new byte[] { 0x00, 0x08, 0x50, 0x48, 0x60, 0x58, 0x70, 0x28, 0x10, 0x68, 0x40, 0x78, 0x20, 0x38, 0x30, 0x18 });
                            break;
                        case (int)CartID.SBP:
                            prom = SBP.Decrypt(prom);
                            break;
                        case (int)CartID.TEOT:
                            prom = TEOT.Pack(prom);
                            break;
                    }

                    if (new[] { 
                        (int)CartID.KOF99, 
                        (int)CartID.GAROU, 
                        (int)CartID.MSLUG3, 
                        (int)CartID.KOF2000 }.Contains(cartID))
                    {
                        string smaName = FindCartSuffix(dir, "sma");
                        FileInfo fS = new FileInfo(smaName);
                        byte[] sma = new BinaryReader(fS.OpenRead()).ReadBytes((int)fS.Length);
                        Buffer.BlockCopy(sma, 0, prom, prom.Length - sma.Length, sma.Length);
                        byte[] moveAround = new byte[0x800000];
                        Buffer.BlockCopy(prom, 0, moveAround, 0, 0x800000);
                        Buffer.BlockCopy(moveAround, 0x700000, prom, 0, 0x100000);
                        Buffer.BlockCopy(moveAround, 0, prom, 0x100000, 0x700000);
                    }

                    Console.WriteLine("...");

                    CreateBinary(dir + Path.DirectorySeparatorChar + cartFilePrefix + ".pd", prom);
                }
            }

            if (cartType == CartType.CMC_42 || cartType == CartType.CMC_50)
            {
                Console.WriteLine("Decoding CROM...");
                byte[] croml = new byte[0];
                byte[] cromh = new byte[0];
                for (int i = 1; i <= 8; i++)
                {
                    string cName = FindCartSuffix(dir, "c" + i);
                    if (File.Exists(cName))
                    {
                        FileInfo f = new FileInfo(cName);
                        byte[] crom1 = ((i & 1) == 1) ? croml : cromh;
                        byte[] crom2 = new BinaryReader(f.OpenRead()).ReadBytes((int)f.Length);
                        byte[] cromn = new byte[crom1.Length + crom2.Length];
                        Buffer.BlockCopy(crom1, 0, cromn, 0, crom1.Length);
                        Buffer.BlockCopy(crom2, 0, cromn, crom1.Length, crom2.Length);
                        if ((i & 1) == 1)
                        {
                            croml = cromn;
                        }
                        else
                        {
                            cromh = cromn;
                        }
                    }
                }
                byte[] cromc = new byte[croml.Length * 2];
                for (int i = 0; i < croml.Length / 2; i++)
                {
                    cromc[i * 4 + 0] = croml[i * 2 + 0];
                    cromc[i * 4 + 1] = cromh[i * 2 + 0];
                    cromc[i * 4 + 2] = croml[i * 2 + 1];
                    cromc[i * 4 + 3] = cromh[i * 2 + 1];
                }
                ProtCMC.GfxDecrypt(cromc, extraXor, cartType == CartType.CMC_50);
                for (int i = 0; i < croml.Length / 2; i++)
                {
                    croml[i * 2 + 0] = cromc[i * 4 + 0];
                    cromh[i * 2 + 0] = cromc[i * 4 + 1];
                    croml[i * 2 + 1] = cromc[i * 4 + 2];
                    cromh[i * 2 + 1] = cromc[i * 4 + 3];
                }
                CreateBinary(dir + Path.DirectorySeparatorChar + cartFilePrefix + ".cl", croml);
                CreateBinary(dir + Path.DirectorySeparatorChar + cartFilePrefix + ".ch", cromh);

                bool isBankedSFix = new[] {
                    (int)CartID.GAROU, 
                    (int)CartID.MSLUG3, 
                    (int)CartID.KOF2000, 
                    (int)CartID.MSLUG4, 
                    (int)CartID.MATRIM, 
                    (int)CartID.SVC, 
                    (int)CartID.KOF2003 }.Contains(cartID);
                byte[] srom = new byte[(isBankedSFix ? 512 : 128) * 1024];
                Console.WriteLine("Decoding SFIX...");
                ProtCMC.SFixDecrypt(cromc, srom);
                CreateBinary(dir + Path.DirectorySeparatorChar + cartFilePrefix + ".sd", srom);
            }

            if (cartType == CartType.CMC_50)
            {
                Console.WriteLine("Decoding M1...");
                string mName = FindCartSuffix(dir, "m1");
                FileInfo f = new FileInfo(mName);
                byte[] mrom = new BinaryReader(f.OpenRead()).ReadBytes((int)f.Length);
                ProtCMC.M1Decrypt(mrom);
                CreateBinary(dir + Path.DirectorySeparatorChar + cartFilePrefix + ".md", mrom);
            }

            if (new[] { 
                (int)CartID.MSLUG4, 
                (int)CartID.ROTD, 
                (int)CartID.KOF2002, 
                (int)CartID.MATRIM, 
                (int)CartID.PNYAA, 
                (int)CartID.MSLUG5, 
                (int)CartID.SVC, 
                (int)CartID.SAMSHO5, 
                (int)CartID.KOF2003, 
                (int)CartID.SAMSH5SP}.Contains(cartID))
            {
                Console.WriteLine("Decoding VROM...");
                byte[] vrom = new byte[0];
                byte[] swapBytes = new byte[8];
                for (int i = 1; i <= 4; i++)
                {
                    string vName = FindCartSuffix(dir, "v" + i);
                    if (File.Exists(vName))
                    {
                        FileInfo f = new FileInfo(vName);
                        byte[] vrom2 = new BinaryReader(f.OpenRead()).ReadBytes((int)f.Length);
                        byte[] vromn = new byte[vrom.Length + vrom2.Length];
                        Buffer.BlockCopy(vrom, 0, vromn, 0, vrom.Length);
                        Buffer.BlockCopy(vrom2, 0, vromn, vrom.Length, vrom2.Length);
                        vrom = vromn;
                    }
                }

                switch (cartID)
                {
                    case (int)CartID.MSLUG4:
                        for (int i = 0; i < vrom.Length / 8; i++)
                        {
                            Buffer.BlockCopy(vrom, i * 8, swapBytes, 0, 4);
                            Buffer.BlockCopy(vrom, i * 8 + 4, vrom, i * 8, 4);
                            Buffer.BlockCopy(swapBytes, 0, vrom, i * 8 + 4, 4);
                        }
                        break;
                    case (int)CartID.ROTD:
                        for (int i = 0; i < vrom.Length / 16; i++)
                        {
                            Buffer.BlockCopy(vrom, i * 16, swapBytes, 0, 8);
                            Buffer.BlockCopy(vrom, i * 16 + 8, vrom, i * 16, 8);
                            Buffer.BlockCopy(swapBytes, 0, vrom, i * 16 + 8, 8);
                        }
                        break;
                    case (int)CartID.PNYAA:
                        for (int i = 0; i < vrom.Length / 4; i++)
                        {
                            Buffer.BlockCopy(vrom, i * 4, swapBytes, 0, 2);
                            Buffer.BlockCopy(vrom, i * 4 + 2, vrom, i * 4, 2);
                            Buffer.BlockCopy(swapBytes, 0, vrom, i * 4 + 2, 2);
                        }
                        break;
                    case (int)CartID.KOF2002:
                        ProtPCM2.Decrypt(vrom, 0);
                        break;
                    case (int)CartID.MATRIM:
                        ProtPCM2.Decrypt(vrom, 1);
                        break;
                    case (int)CartID.MSLUG5:
                        ProtPCM2.Decrypt(vrom, 2);
                        break;
                    case (int)CartID.SVC:
                        ProtPCM2.Decrypt(vrom, 3);
                        break;
                    case (int)CartID.SAMSHO5:
                        ProtPCM2.Decrypt(vrom, 4);
                        break;
                    case (int)CartID.KOF2003:
                        ProtPCM2.Decrypt(vrom, 5);
                        break;
                    case (int)CartID.SAMSH5SP:
                        ProtPCM2.Decrypt(vrom, 6);
                        if (vrom[0x06bc0] == 0xab &&
                            vrom[0x0ed41] == 0x08 &&
                            vrom[0x16bc0] == 0x6e &&
                            vrom[0x1ed41] == 0xda)
                        {
                            vrom[0x06bc0] = 0x08;
                            vrom[0x0ed41] = 0x89;
                            vrom[0x16bc0] = 0x82;
                            vrom[0x1ed41] = 0x8f;
                        }
                        break;
                }

                byte[] vroma = vrom;
                byte[] vromb = new byte[0];
                if (vrom.Length > 0x800000)
                {
                    vroma = new byte[0x800000];
                    Buffer.BlockCopy(vrom, 0, vroma, 0, vroma.Length);
                    vromb = new byte[vrom.Length - 0x800000];
                    Buffer.BlockCopy(vrom, 0x800000, vromb, 0, vromb.Length);
                }
                CreateBinary(dir + Path.DirectorySeparatorChar + cartFilePrefix + ".va", vroma);
                if (vromb.Length > 0)
                {
                    CreateBinary(dir + Path.DirectorySeparatorChar + cartFilePrefix + ".vb", vromb);
                }
            }
        }

        static string FindCartFile(string dir, string romName)
        {
            foreach (string s in Directory.GetFiles(dir))
            {
                string s2 = s.Substring(s.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                if (s2.ToUpper() == romName.ToUpper())
                {
                    return s;
                }
            }
            return "";
        }

        static string FindCartSuffix(string dir, string romName)
        {
            foreach (string s in Directory.GetFiles(dir))
            {
                string s2 = s.Substring(s.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                if (s2.ToUpper().IndexOf("-" + romName.ToUpper()) != -1 ||
                    s2.ToUpper().IndexOf("." + romName.ToUpper()) != -1 ||
                    s2.ToUpper() == romName.ToUpper() ||
                    s2.ToUpper() == romName.ToUpper() + ".BIN" ||
                    s2.ToUpper() == romName.ToUpper() + ".ROM")
                {
                    return s;
                }
            }
            return "";
        }

        static void ParseCROMBytes(string dir, int first, int pieces)
        {
            int len = (int)(new FileInfo(FindCartSuffix(dir, "c" + first)).Length);
            byte[] croml = new byte[len * pieces / 2];
            byte[] cromh = new byte[len * pieces / 2];
            for (int i = 0; i < pieces; i++)
            {
                byte[] data = new BinaryReader(new FileInfo(FindCartSuffix(dir, "c" + (first + i).ToString())).OpenRead()).ReadBytes(len);
                for (int x = 0; x < len; x++)
                {
                    if ((i & 2) == 0)
                    {
                        croml[((i & 4) * len / 2) + x * 2 + (i & 1)] = data[x];
                    }
                    else
                    {
                        cromh[((i & 4) * len / 2) + x * 2 + (i & 1)] = data[x];
                    }
                }
            }
            CreateBinary(dir + Path.DirectorySeparatorChar + "cl", croml);
            CreateBinary(dir + Path.DirectorySeparatorChar + "ch", cromh);
        }

        static void ScanCart(string dir)
        {
            string p1Name = FindCartSuffix(dir, "p1");        
            string p2Name = FindCartSuffix(dir, "p2");
            string sbpName = FindCartSuffix(dir, "u13");

            if (p1Name.Length == 0 && p2Name.Length == 0)
            {
                p1Name = FindCartSuffix(dir, "podd");
                p2Name = FindCartSuffix(dir, "peven");
            }
            if (p1Name.Length == 0 && p2Name.Length == 0)
            {
                p1Name = FindCartSuffix(dir, "ep1");
                p2Name = FindCartSuffix(dir, "ep2");
            }
            if (p1Name.Length == 0 && p2Name.Length == 0)
            {
                p1Name = FindCartSuffix(dir, "sp1");
                p2Name = FindCartSuffix(dir, "sp2");
            }

            int cartID = 0;
            string cartIDStr = p1Name.Substring(p1Name.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            bool programIsBytes = false;
            for (int i = 0; i < cartIDStr.Length; i++)
            {
                if (!char.IsDigit(cartIDStr[i]))
                {
                    if (i > 0)
                    {
                        cartID = int.Parse(cartIDStr.Substring(0, i));
                    }
                    break;
                }
            }
            if (sbpName.Length > 0)
            {
                p1Name = FindCartSuffix(dir, "02a");
            }
            if (cartID == 0 && p1Name.Length > 0)
            {
                // could not determine cart num from filename, so look inside
                FileInfo p1 = new FileInfo(p1Name);
                FileStream p1fs = p1.OpenRead();
                byte[] data = new byte[8];
                int headerOffset = 0;

                // first, find NEO GEO ID
                if (p2Name.Length == 0 && p1.Length > 0x100000)
                {
                    headerOffset = 0x100000;
                }
                p1fs.Seek(headerOffset + 0x100, SeekOrigin.Begin);
                p1fs.Read(data);
                if (data.Take(6).SequenceEqual(NEO_GEO_HEADER))
                {
                    // found a valid header, now identify some games
                    p1fs.Seek(headerOffset + 0x108, SeekOrigin.Begin);
                    int cartIDLo = p1fs.ReadByte();
                    int cartIDHi = p1fs.ReadByte();
                    if (cartIDLo == 0xdc && cartIDHi == 0xfe)
                    {
                        cartID = (int)CartID.SBP; // super bubble pop
                        File.Copy(FindCartSuffix(dir, "01b"), dir + Path.DirectorySeparatorChar + "sbp.m1", true);
                        File.Copy(FindCartSuffix(dir, "02a"), dir + Path.DirectorySeparatorChar + "sbp.p1", true);
                        File.Copy(FindCartSuffix(dir, "02b"), dir + Path.DirectorySeparatorChar + "sbp.s1", true);
                        File.Copy(FindCartSuffix(dir, "03b"), dir + Path.DirectorySeparatorChar + "sbp.c1", true);
                        File.Copy(FindCartSuffix(dir, "04b"), dir + Path.DirectorySeparatorChar + "sbp.c2", true);
                        File.Copy(FindCartSuffix(dir, "12a"), dir + Path.DirectorySeparatorChar + "sbp.v1", true);
                        File.Copy(FindCartSuffix(dir, "13a"), dir + Path.DirectorySeparatorChar + "sbp.v2", true);
                    }
                    else if (cartIDLo == 0x34 && cartIDHi == 0x12)
                    {
                        cartID = (int)CartID.TEOT;
                    }
                    else
                    {
                        cartID = cartIDHi * 100 + (cartIDLo >> 4) * 10 + (cartIDLo & 15);
                    }
                } else
                {
                    // didn't find a header .. is the program arranged in bytes?
                    p1fs.Seek(headerOffset + 0x80, SeekOrigin.Begin);
                    p1fs.Read(data);
                    if (data.Take(3).SequenceEqual(NEO_GEO_HEADER_ODD) && 
                        new FileInfo(p1Name).Length == new FileInfo(p2Name).Length)
                    {
                        programIsBytes = true;
                    }
                }
            }

            if (programIsBytes)
            {
                Console.WriteLine("Combined bytes...");
                int len = (int)(new FileInfo(p1Name).Length);
                byte[] prom1 = new BinaryReader(new FileInfo(p1Name).OpenRead()).ReadBytes(len);
                byte[] prom2 = new BinaryReader(new FileInfo(p2Name).OpenRead()).ReadBytes(len);
                byte[] prom = new byte[prom1.Length * 2];
                for (int i = 0; i < prom1.Length; i++)
                {
                    prom[i * 2 + 0] = prom1[i];
                    prom[i * 2 + 1] = prom2[i];
                }
                CreateBinary(dir + Path.DirectorySeparatorChar + "pd", prom);

                if (FindCartSuffix(dir, "c1").Length > 0)
                {
                    int pieces = 4;
                    if (FindCartSuffix(dir, "c5").Length > 0) { pieces = 8; }
                    if (FindCartSuffix(dir, "c9").Length > 0) { pieces = 12; }
                    ParseCROMBytes(dir, 1, pieces);
                }
                else if (FindCartSuffix(dir, "c11").Length > 0)
                {
                    int pieces = 4;
                    if (FindCartSuffix(dir, "c15").Length > 0) { pieces = 8; }
                    ParseCROMBytes(dir, 11, pieces);
                }
            }

            try
            {
                switch (cartID)
                {
                    case (int)CartID.JOCKEY:   ParseCart(dir, cartID, CartType.CMC_50,  0xac, "jockeygp"); break;
                    case (int)CartID.ZUPAPA:   ParseCart(dir, cartID, CartType.CMC_42,  0xbd, "zupapa"); break;
                    case (int)CartID.KOF98:    ParseCart(dir, cartID, CartType.GENERIC, 0x00, "kof98"); break;
                    case (int)CartID.KOF99:    ParseCart(dir, cartID, CartType.CMC_42,  0x00, "kof99"); break;
                    case (int)CartID.GANRYU:   ParseCart(dir, cartID, CartType.CMC_42,  0x07, "ganryu"); break;
                    case (int)CartID.GAROU:    ParseCart(dir, cartID, CartType.CMC_42,  0x06, "garou"); break;
                    case (int)CartID.S1945P:   ParseCart(dir, cartID, CartType.CMC_42,  0x05, "s1945p"); break;
                    case (int)CartID.PREISLE2: ParseCart(dir, cartID, CartType.CMC_42,  0x9f, "preisle2"); break;
                    case (int)CartID.MSLUG3:   ParseCart(dir, cartID, CartType.CMC_42,  0xad, "mslug3"); break;
                    case (int)CartID.BANGBEAD: ParseCart(dir, cartID, CartType.CMC_42,  0xf8, "bangbead"); break;
                    case (int)CartID.NITD:     ParseCart(dir, cartID, CartType.CMC_42,  0xff, "nitd"); break;
                    case (int)CartID.SENGOKU3: ParseCart(dir, cartID, CartType.CMC_42,  0xfe, "sengoku3"); break;
                    case (int)CartID.KOF2000:  ParseCart(dir, cartID, CartType.CMC_50,  0x00, "kof2000"); break;
                    case (int)CartID.KOF2001:  ParseCart(dir, cartID, CartType.CMC_50,  0x1e, "kof2001"); break;
                    case (int)CartID.MSLUG4:   ParseCart(dir, cartID, CartType.CMC_50,  0x31, "mslug4"); break;
                    case (int)CartID.ROTD:     ParseCart(dir, cartID, CartType.CMC_50,  0x3f, "rotd"); break;
                    case (int)CartID.KOF2002:  ParseCart(dir, cartID, CartType.CMC_50,  0xec, "kof2002"); break;
                    case (int)CartID.MATRIM:   ParseCart(dir, cartID, CartType.CMC_50,  0x6a, "matrim"); break;
                    case (int)CartID.PNYAA:    ParseCart(dir, cartID, CartType.CMC_50,  0x2e, "pnyaa"); break;
                    case (int)CartID.MSLUG5:   ParseCart(dir, cartID, CartType.CMC_50,  0x19, "mslug5"); break;
                    case (int)CartID.SVC:      ParseCart(dir, cartID, CartType.CMC_50,  0x57, "svc"); break;
                    case (int)CartID.SAMSHO5:  ParseCart(dir, cartID, CartType.CMC_50,  0x0f, "samsho5"); break;
                    case (int)CartID.KOF2003:  ParseCart(dir, cartID, CartType.CMC_50,  0x9d, "kof2003"); break;
                    case (int)CartID.SAMSH5SP: ParseCart(dir, cartID, CartType.CMC_50,  0x0d, "samsh5sp"); break;
                    case (int)CartID.SBP:      ParseCart(dir, cartID, CartType.GENERIC, 0x00, "sbp"); break;
                    case (int)CartID.TEOT:     ParseCart(dir, cartID, CartType.GENERIC, 0x00, "teot"); break;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error parsing " + dir);
            }
        }

        static void ParseDarkSoft(string dir)
        {
            Console.WriteLine("Found DarkSoft");
            foreach (string s in Directory.GetFiles(dir))
            {
                switch (s.Substring(s.LastIndexOf(Path.DirectorySeparatorChar) + 1))
                {
                    case "crom0":
                        Console.WriteLine("Decoding CROM...");
                        FileInfo f = new FileInfo(s);
                        byte[] crom = new BinaryReader(f.OpenRead()).ReadBytes((int)f.Length);
                        byte[] croml = new byte[crom.Length / 2];
                        byte[] cromh = new byte[crom.Length / 2];
                        for (int i = 0; i < crom.Length / 4; i++)
                        {
                            croml[i * 2 + 0] = crom[i * 4 + 0];
                            croml[i * 2 + 1] = crom[i * 4 + 1];
                            cromh[i * 2 + 0] = crom[i * 4 + 2];
                            cromh[i * 2 + 1] = crom[i * 4 + 3];
                        }
                        CreateBinary(dir + Path.DirectorySeparatorChar + "crom.c1", croml);
                        CreateBinary(dir + Path.DirectorySeparatorChar + "crom.c2", cromh);
                        break;
                    case "m1rom":
                        File.Move(s, s + ".m1");
                        break;
                    case "prom":
                        File.Move(s, s + ".p1");
                        break;
                    case "srom":
                        File.Move(s, s + ".s1");
                        break;
                    case "vroma0":
                        File.Move(s, s + ".v1");
                        break;
                }
            }
        }

        static UInt32 BytesToUInt32(byte[] b, int offset)
        {
            UInt32 result = b[offset + 3];
            result <<= 8;
            result |= b[offset + 2];
            result <<= 8;
            result |= b[offset + 1];
            result <<= 8;
            result |= b[offset];
            return result;
        }

        static void ParseNeoSD(string name)
        {
            FileInfo f = new FileInfo(name);
            BinaryReader br = new BinaryReader(f.OpenRead());
            byte[] header = br.ReadBytes(4096);
            if (header.Take(4).SequenceEqual(NEO_SD_HEADER))
            {
                string prefix = f.Name.Substring(0, f.Name.Length - 4);
                int plen =  (int)BytesToUInt32(header, 4);
                int slen =  (int)BytesToUInt32(header, 8);
                int mlen =  (int)BytesToUInt32(header, 12);
                int v1len = (int)BytesToUInt32(header, 16);
                int v2len = (int)BytesToUInt32(header, 20);
                int clen =  (int)BytesToUInt32(header, 24);
                string title = "";
                for (int i = 44; i < 76; i++)
                {
                    if (header[i] == 0)
                    {
                        break;
                    }
                    if (("" + (char)header[i]).IndexOfAny(Path.GetInvalidFileNameChars()) == -1)
                    {
                        title += (char)header[i];
                    }
                }
                if (title.Length == 0)
                {
                    title = prefix;
                }
                Console.WriteLine("Unpacking NEO SD (" + title + ")");
                DirectoryInfo di = Directory.CreateDirectory(f.DirectoryName + Path.DirectorySeparatorChar + title);
                CreateBinary(di.FullName + Path.DirectorySeparatorChar + prefix + ".pd", br.ReadBytes(plen));
                CreateBinary(di.FullName + Path.DirectorySeparatorChar + prefix + ".sd", br.ReadBytes(slen));
                CreateBinary(di.FullName + Path.DirectorySeparatorChar + prefix + ".md", br.ReadBytes(mlen));
                CreateBinary(di.FullName + Path.DirectorySeparatorChar + prefix + ".va", br.ReadBytes(v1len));
                if (v2len > 0) CreateBinary(di.FullName + Path.DirectorySeparatorChar + prefix + ".vb", br.ReadBytes(v2len));
                byte[] crom = br.ReadBytes(clen);
                byte[] croml = new byte[crom.Length / 2];
                byte[] cromh = new byte[crom.Length / 2];
                for (int i = 0; i < crom.Length / 2; i++)
                {
                    croml[i] = crom[i * 2 + 0];
                    cromh[i] = crom[i * 2 + 1];
                }
                CreateBinary(di.FullName + Path.DirectorySeparatorChar + prefix + ".cl", croml);
                CreateBinary(di.FullName + Path.DirectorySeparatorChar + prefix + ".ch", cromh);
            }
        }

        static void ScanDir(string dir)
        {
            try
            {
                foreach (string s in Directory.GetDirectories(dir))
                {
                    ScanDir(s);
                }
                Console.WriteLine("Scanning: " + dir);
                if (FindCartFile(dir, "crom0").Length > 0)
                {
                    ParseDarkSoft(dir);
                }
                else if (FindCartSuffix(dir, "p1").Length > 0 || FindCartSuffix(dir, "u13").Length > 0)
                {
                    ScanCart(dir);
                }
                else
                {
                    // Didn't find individual game files; any all-in-one files?
                    foreach (string s in Directory.GetFiles(dir))
                    {
                        if (s.ToUpper().EndsWith(".NEO"))
                        {
                            ParseNeoSD(s);
                        }
                    }
                }
            }
#if DEBUG
            catch (Exception e)
            {
                Console.WriteLine("Error scanning " + dir + ": " + e.ToString());
            }
#else
            catch (Exception)
            {
                Console.WriteLine("Error scanning " + dir);
            }
#endif
        }

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                ScanDir(args[0]);
            }
            else 
            {
                Console.WriteLine("BackBit Neo Decode v" + VERSION);
                Console.WriteLine();
                Console.WriteLine("Usage: neodecode <pathname>");
                Console.WriteLine("This program will scan the specified path and decode all found Neo Geo games");
                Console.WriteLine("so they can be played on a BackBit Neo Geo Platinum cartridge.");
            }
        }
    }
}