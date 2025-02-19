
using System;
using System.Linq;
using System.Xml.Linq;

namespace NeoDecode
{
    internal class Program
    {
        enum CartType
        {
            GENERIC,
            CMC_42,
            CMC_50
        }

        static readonly byte[] NEO_GEO_HEADER = { 0x45, 0x4E, 0x2D, 0x4F, 0x45, 0x47 };

        static void ParseCart(string dir, int cartNum, CartType cartType, int extraXor, string name)
        {
            string cartNumPadded = cartNum.ToString();
            while (cartNumPadded.Length < 3)
            {
                cartNumPadded = "0" + cartNumPadded;
            }

            Console.WriteLine("Found: " + name);

            if (new[] { 242, 251, 253, 256, 257, 265, 266, 268, 269, 270, 271, 272, 999 }.Contains(cartNum))
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
                    new[] { 251, 253, 256, 257 }.Contains(cartNum))
                {
                    Console.WriteLine(" (Not Encrypted)...");
                }
                else
                {
                    int smaOffset = 0;
                    switch (cartNum)
                    {
                        case 242: // kof98
                            prom = ProtCMC.KOF98Decrypt(prom);
                            break;
                        case 251: // kof99
                            ProtCMC.P1Decrypt(prom, new byte[] { 13, 7, 3, 0, 9, 4, 5, 6, 1, 12, 8, 14, 10, 11, 2, 15 });
                            break;
                        case 253: // garou
                            if (prom[0] == 0xf5)
                            {
                                Console.Write(" (Alternate)");
                                ProtCMC.P1Decrypt(prom, new byte[] { 14, 5, 1, 11, 7, 4, 10, 15, 3, 12, 8, 13, 0, 2, 9, 6 });
                                smaOffset = 0x300000; // need an empty spot
                            }
                            else
                            {
                                ProtCMC.P1Decrypt(prom, new byte[] { 13, 12, 14, 10, 8, 2, 3, 1, 5, 9, 11, 4, 15, 0, 6, 7 });
                            }
                            break;
                        case 256: // mslug3
                            if (prom[2] == 0xfb)
                            {
                                Console.Write(" (Alternate)");
                                ProtCMC.P1Decrypt(prom, new byte[] { 2, 11, 12, 14, 9, 3, 1, 4, 13, 7, 6, 8, 10, 15, 0, 5 }); // A
                            }
                            else
                            {
                                ProtCMC.P1Decrypt(prom, new byte[] { 4, 11, 14, 3, 1, 13, 0, 7, 2, 8, 12, 15, 10, 9, 5, 6 });
                            }
                            break;
                        case 257: // kof2000
                            ProtCMC.P1Decrypt(prom, new byte[] { 12, 8, 11, 3, 15, 14, 7, 0, 10, 13, 6, 5, 9, 2, 1, 4 });
                            break;
                        case 265: // kof2002
                        case 266: // matrim
                            ProtCMC.P1Swap(prom, new byte[] { 0x00, 0x08, 0x20, 0x38, 0x40, 0x28, 0x10, 0x48, 0x30, 0x18 });
                            break;
                        case 268: // mslug5
                            ProtCMC.MSlug5Decrypt(prom);
                            break;
                        case 269: // svc
                            ProtCMC.SVCDecrypt(prom);
                            break;
                        case 270: // samsho5
                            ProtCMC.P1Swap(prom, new byte[] { 0x00, 0x08, 0x70, 0x68, 0x50, 0x18, 0x20, 0x48, 0x30, 0x78, 0x60, 0x28, 0x10, 0x58, 0x40, 0x38 });
                            break;
                        case 271: // kof2003
                            if (prom[0] == 0xd2)
                            {
                                Console.Write(" (Alternate)");
                                prom = ProtCMC.KOF2003HDecrypt(prom);
                            }
                            else
                            {
                                prom = ProtCMC.KOF2003Decrypt(prom);
                            }
                            break;
                        case 272: // samsh5sp
                            ProtCMC.P1Swap(prom, new byte[] { 0x00, 0x08, 0x50, 0x48, 0x60, 0x58, 0x70, 0x28, 0x10, 0x68, 0x40, 0x78, 0x20, 0x38, 0x30, 0x18 });
                            break;
                        case 999: // sbp
                            prom = ProtCMC.SBPDecrypt(prom);
                            break;
                    }

                    if (new[] { 251, 253, 256, 257 }.Contains(cartNum))
                    {
                        string smaName = FindCartSuffix(dir, "sma");
                        FileInfo fS = new FileInfo(smaName);
                        byte[] sma = new BinaryReader(fS.OpenRead()).ReadBytes((int)fS.Length);
                        Buffer.BlockCopy(sma, 0, prom, prom.Length - smaOffset - sma.Length, sma.Length);
                    }

                    Console.WriteLine("...");

                    FileStream fPROM = File.Create(dir + Path.DirectorySeparatorChar + cartNumPadded + ".pd");
                    fPROM.Write(prom);
                    fPROM.Close();
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
                FileStream fCROML = File.Create(dir + Path.DirectorySeparatorChar + cartNumPadded + ".cl");
                fCROML.Write(croml);
                fCROML.Close();
                FileStream fCROMH = File.Create(dir + Path.DirectorySeparatorChar + cartNumPadded + ".ch");
                fCROMH.Write(cromh);
                fCROMH.Close();

                bool isBankedSFix = new[] { 253, 256, 257, 263, 266, 269, 271 }.Contains(cartNum);
                byte[] srom = new byte[(isBankedSFix ? 512 : 128) * 1024];
                Console.WriteLine("Decoding SFIX...");
                ProtCMC.SFixDecrypt(cromc, srom);
                FileStream fSROM = File.Create(dir + Path.DirectorySeparatorChar + cartNumPadded + ".sd");
                fSROM.Write(srom);
                fSROM.Close();
            }

            if (cartType == CartType.CMC_50)
            {
                Console.WriteLine("Decoding M1...");
                string mName = FindCartSuffix(dir, "m1");
                FileInfo f = new FileInfo(mName);
                byte[] mrom = new BinaryReader(f.OpenRead()).ReadBytes((int)f.Length);
                ProtCMC.M1Decrypt(mrom);
                FileStream fMROM = File.Create(dir + Path.DirectorySeparatorChar + cartNumPadded + ".md");
                fMROM.Write(mrom);
                fMROM.Close();
            }

            if (new[] { 263, 264, 265, 266, 267, 268, 269, 270, 271, 272 }.Contains(cartNum))
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

                switch (cartNum)
                {
                    case 263: // mslug4
                        for (int i = 0; i < vrom.Length / 8; i++)
                        {
                            Buffer.BlockCopy(vrom, i * 8, swapBytes, 0, 4);
                            Buffer.BlockCopy(vrom, i * 8 + 4, vrom, i * 8, 4);
                            Buffer.BlockCopy(swapBytes, 0, vrom, i * 8 + 4, 4);
                        }
                        break;
                    case 264: // rotd
                        for (int i = 0; i < vrom.Length / 16; i++)
                        {
                            Buffer.BlockCopy(vrom, i * 16, swapBytes, 0, 8);
                            Buffer.BlockCopy(vrom, i * 16 + 8, vrom, i * 16, 8);
                            Buffer.BlockCopy(swapBytes, 0, vrom, i * 16 + 8, 8);
                        }
                        break;
                    case 267: // pnyaa
                        for (int i = 0; i < vrom.Length / 4; i++)
                        {
                            Buffer.BlockCopy(vrom, i * 4, swapBytes, 0, 2);
                            Buffer.BlockCopy(vrom, i * 4 + 2, vrom, i * 4, 2);
                            Buffer.BlockCopy(swapBytes, 0, vrom, i * 4 + 2, 2);
                        }
                        break;
                    case 265: // kof2002
                        ProtCMC.VoiceDecrypt(vrom, 0);
                        break;
                    case 266: // matrim
                        ProtCMC.VoiceDecrypt(vrom, 1);
                        break;
                    case 268: // mslug5
                        ProtCMC.VoiceDecrypt(vrom, 2);
                        break;
                    case 269: // svc
                        ProtCMC.VoiceDecrypt(vrom, 3);
                        break;
                    case 270: // samsho5
                        ProtCMC.VoiceDecrypt(vrom, 4);
                        break;
                    case 271: // kof2003
                        ProtCMC.VoiceDecrypt(vrom, 5);
                        break;
                    case 272: // samsh5sp
                        ProtCMC.VoiceDecrypt(vrom, 6);
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
                FileStream fVROM = File.Create(dir + Path.DirectorySeparatorChar + cartNumPadded + ".va");
                fVROM.Write(vroma);
                fVROM.Close();

                if (vromb.Length > 0)
                {
                    fVROM = File.Create(dir + Path.DirectorySeparatorChar + cartNumPadded + ".vb");
                    fVROM.Write(vromb);
                    fVROM.Close();
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
                    s2.ToUpper().IndexOf("." + romName.ToUpper()) != -1)
                {
                    return s;
                }
            }
            return "";
        }

        static void ScanCart(string dir)
        {
            string p1Name = FindCartSuffix(dir, "p1");
            string p2Name = FindCartSuffix(dir, "p2");
            string sbpName = FindCartSuffix(dir, "u13");

            int cartNum = 0;
            string cartNumStr = p1Name.Substring(p1Name.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            for (int i = 0; i < cartNumStr.Length; i++)
            {
                if (!char.IsDigit(cartNumStr[i]))
                {
                    if (i > 0)
                    {
                        cartNum = int.Parse(cartNumStr.Substring(0, i));
                    }
                    break;
                }
            }
            if (sbpName.Length > 0)
            {
                p1Name = FindCartSuffix(dir, "02a");
            }
            if (cartNum == 0 && p1Name.Length > 0)
            {
                // could not determine cart num from filename, so look inside
                FileInfo p1 = new FileInfo(p1Name);
                FileStream p1fs = p1.OpenRead();
                if (p2Name.Length == 0 && p1.Length > 0x100000)
                {
                    p1fs.Seek(0x100000, SeekOrigin.Begin);
                }
                p1fs.Seek(0x108, SeekOrigin.Current);
                int cartNumLo = p1fs.ReadByte();
                int cartNumHi = p1fs.ReadByte();
                if (cartNumLo == 0xdc && cartNumHi == 0xfe)
                {
                    cartNum = 999; // super bubble pop
                    File.Copy(FindCartSuffix(dir, "01b"), dir + Path.DirectorySeparatorChar + cartNum + ".m1");
                    File.Copy(FindCartSuffix(dir, "02a"), dir + Path.DirectorySeparatorChar + cartNum + ".p1");
                    File.Copy(FindCartSuffix(dir, "02b"), dir + Path.DirectorySeparatorChar + cartNum + ".s1");
                    File.Copy(FindCartSuffix(dir, "03b"), dir + Path.DirectorySeparatorChar + cartNum + ".c1");
                    File.Copy(FindCartSuffix(dir, "04b"), dir + Path.DirectorySeparatorChar + cartNum + ".c2");
                    File.Copy(FindCartSuffix(dir, "12a"), dir + Path.DirectorySeparatorChar + cartNum + ".v1");
                    File.Copy(FindCartSuffix(dir, "13a"), dir + Path.DirectorySeparatorChar + cartNum + ".v2");
                }
                else
                {
                    cartNum = cartNumHi * 100 + (cartNumLo >> 4) * 10 + (cartNumLo & 15);
                }
            }

            try
            {
                switch (cartNum)
                {
                    case 70: ParseCart(dir, cartNum, CartType.CMC_42, 0xbd, "zupapa"); break;
                    case 242: ParseCart(dir, cartNum, CartType.GENERIC, 0x00, "kof98"); break;
                    case 251: ParseCart(dir, cartNum, CartType.CMC_42, 0x00, "kof99"); break;
                    case 252: ParseCart(dir, cartNum, CartType.CMC_42, 0x07, "ganryu"); break;
                    case 253: ParseCart(dir, cartNum, CartType.CMC_42, 0x06, "garou"); break;
                    case 254: ParseCart(dir, cartNum, CartType.CMC_42, 0x05, "s1945p"); break;
                    case 255: ParseCart(dir, cartNum, CartType.CMC_42, 0x9f, "preisle2"); break;
                    case 256: ParseCart(dir, cartNum, CartType.CMC_42, 0xad, "mslug3"); break;
                    case 259: ParseCart(dir, cartNum, CartType.CMC_42, 0xf8, "bangbead"); break;
                    case 260: ParseCart(dir, cartNum, CartType.CMC_42, 0xff, "nitd"); break;
                    case 261: ParseCart(dir, cartNum, CartType.CMC_42, 0xfe, "sengoku3"); break;
                    case 8: ParseCart(dir, cartNum, CartType.CMC_50, 0xac, "jockeygp"); break;
                    case 257: ParseCart(dir, cartNum, CartType.CMC_50, 0x00, "kof2000"); break;
                    case 262: ParseCart(dir, cartNum, CartType.CMC_50, 0x1e, "kof2001"); break;
                    case 263: ParseCart(dir, cartNum, CartType.CMC_50, 0x31, "mslug4"); break;
                    case 264: ParseCart(dir, cartNum, CartType.CMC_50, 0x3f, "rotd"); break;
                    case 265: ParseCart(dir, cartNum, CartType.CMC_50, 0xec, "kof2002"); break;
                    case 266: ParseCart(dir, cartNum, CartType.CMC_50, 0x6a, "matrim"); break;
                    case 267: ParseCart(dir, cartNum, CartType.CMC_50, 0x2e, "pnyaa"); break;
                    case 268: ParseCart(dir, cartNum, CartType.CMC_50, 0x19, "mslug5"); break;
                    case 269: ParseCart(dir, cartNum, CartType.CMC_50, 0x57, "svc"); break;
                    case 270: ParseCart(dir, cartNum, CartType.CMC_50, 0x0f, "samsho5"); break;
                    case 271: ParseCart(dir, cartNum, CartType.CMC_50, 0x9d, "kof2003"); break;
                    case 272: ParseCart(dir, cartNum, CartType.CMC_50, 0x0d, "samsh5sp"); break;
                    case 999: ParseCart(dir, cartNum, CartType.GENERIC, 0x00, "sbp"); break;
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
                        for (int i = 0; i < crom.Length / 4; i++) {
                            croml[i * 2 + 0] = crom[i * 4 + 0];
                            croml[i * 2 + 1] = crom[i * 4 + 1];
                            cromh[i * 2 + 0] = crom[i * 4 + 2];
                            cromh[i * 2 + 1] = crom[i * 4 + 3];
                        }
                        FileStream fCROML = File.Create(dir + Path.DirectorySeparatorChar + "crom.c1");
                        fCROML.Write(croml);
                        fCROML.Close();
                        FileStream fCROMH = File.Create(dir + Path.DirectorySeparatorChar + "crom.c2");
                        fCROMH.Write(cromh);
                        fCROMH.Close();
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
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine("Error scanning " + dir + ": " + e.ToString());
#else
                Console.WriteLine("Error scanning " + dir);
#endif
            }
        }

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                ScanDir(args[0]);
            }
            else 
            {
                Console.WriteLine("Usage: neodecode <pathname>");
                Console.WriteLine("This program will scan the specified path and decode all found Neo Geo games");
                Console.WriteLine("so they can be played on a BackBit Neo Geo Platinum cartridge.");
            }
        }
    }
}