
using System;
using System.Linq;

namespace NeoDecode
{
    internal class Program
    {
        enum CartType
        {
            CMC_42,
            CMC_50
        }

        static void ParseCart(string dir, int cartNum, CartType cartType, int extraXor, string name)
        {
            byte[] croml = new byte[0];
            byte[] cromh = new byte[0];
            Console.WriteLine("Scanning: " + dir);
            Console.WriteLine("Found: " + name);

            for (int i = 1; i <= 8; i++)
            {
                string cName = FindCartFile(dir, "c" + i);
                if (File.Exists(cName)) 
                { 
                    FileInfo f = new FileInfo(cName);
                    byte[] crom1 = ((i & 1) == 1) ? croml : cromh;
                    byte[] crom2 = new BinaryReader(f.OpenRead()).ReadBytes((int)f.Length);
                    byte[] cromn = new byte[crom1.Length + crom2.Length];
                    System.Buffer.BlockCopy(crom1, 0, cromn, 0, crom1.Length);
                    System.Buffer.BlockCopy(crom2, 0, cromn, crom1.Length, crom2.Length);
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

            Console.WriteLine("Decoding CROM...");
            byte[] cromc = new byte[croml.Length * 2];
            for (int i = 0; i < croml.Length / 2; i++)
            {
                cromc[i * 4 + 0] = croml[i * 2 + 0];
                cromc[i * 4 + 1] = croml[i * 2 + 1];
                cromc[i * 4 + 2] = cromh[i * 2 + 0];
                cromc[i * 4 + 3] = cromh[i * 2 + 1];
            }
            ProtCMC.GfxDecrypt(cromc, extraXor, cartType == CartType.CMC_50);
            for (int i = 0; i < croml.Length / 2; i++)
            {
                croml[i * 2 + 0] = cromc[i * 4 + 0];
                croml[i * 2 + 1] = cromc[i * 4 + 1];
                cromh[i * 2 + 0] = cromc[i * 4 + 2];
                cromh[i * 2 + 1] = cromc[i * 4 + 3];
            }
            FileStream fCROML = File.OpenWrite(dir + Path.DirectorySeparatorChar + cartNum + ".c1d");
            fCROML.Write(croml);
            fCROML.Close();
            FileStream fCROMH = File.OpenWrite(dir + Path.DirectorySeparatorChar + cartNum + ".c2d");
            fCROMH.Write(cromh);
            fCROMH.Close();

            bool isBankedSFix = new [] { 253, 256, 257, 263, 266, 269, 271 }.Contains(cartNum);
            byte[] srom = new byte[(isBankedSFix ? 512 : 128) * 1024];
            Console.WriteLine("Decoding SFIX...");
            ProtCMC.SFixDecrypt(cromc, srom);
            FileStream fSROM = File.OpenWrite(dir + Path.DirectorySeparatorChar + cartNum + ".sd");
            fSROM.Write(srom);
            fSROM.Close();

            if (cartType == CartType.CMC_50)
            {
                Console.WriteLine("Decoding M1...");
                string mName = FindCartFile(dir, "m1");
                FileInfo f = new FileInfo(mName);
                byte[] mrom = new BinaryReader(f.OpenRead()).ReadBytes((int)f.Length);
                ProtCMC.M1Decrypt(mrom);
                FileStream fMROM = File.OpenWrite(dir + Path.DirectorySeparatorChar + cartNum + ".md");
                fMROM.Write(mrom);
                fMROM.Close();
            }
        }

        static string FindCartFile(string dir, string romName)
        {
            foreach (string s in Directory.GetFiles(dir))
            {
                if (s.ToUpper().IndexOf("-" + romName.ToUpper()) != -1 ||
                    s.ToUpper().IndexOf("." + romName.ToUpper()) != -1)
                {
                    return s;
                }
            }
            return "";
        }

        static void ScanCart(string dir)
        {
            string p1Name = FindCartFile(dir, "p1");
            string p2Name = FindCartFile(dir, "p2");

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
                cartNum = cartNumHi * 100 + (cartNumLo >> 4) * 10 + (cartNumLo & 15);
            }
            
            switch (cartNum)
            {
                case 70:  ParseCart(dir, cartNum, CartType.CMC_42, 0xbd, "zupapa"); break;
                case 251: ParseCart(dir, cartNum, CartType.CMC_42, 0x00, "kof99"); break;
                case 252: ParseCart(dir, cartNum, CartType.CMC_42, 0x07, "ganryu"); break;
                case 253: ParseCart(dir, cartNum, CartType.CMC_42, 0x06, "garou"); break;
                case 254: ParseCart(dir, cartNum, CartType.CMC_42, 0x05, "s1945p"); break;
                case 255: ParseCart(dir, cartNum, CartType.CMC_42, 0x9f, "preisle2"); break;
                case 256: ParseCart(dir, cartNum, CartType.CMC_42, 0xad, "mslug3"); break;
                case 259: ParseCart(dir, cartNum, CartType.CMC_42, 0xf8, "bangbead"); break;
                case 260: ParseCart(dir, cartNum, CartType.CMC_42, 0xff, "nitd"); break;
                case 261: ParseCart(dir, cartNum, CartType.CMC_42, 0xfe, "sengoku3"); break;
                case 8:   ParseCart(dir, cartNum, CartType.CMC_50, 0xac, "jockeygp"); break;
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
            }
        }

        static void ScanDir(string dir)
        {
            foreach (string s in Directory.GetDirectories(dir)) 
            {
                ScanDir(s);
            }
            if (FindCartFile(dir, "p1").Length > 0) 
            {
                ScanCart(dir);
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