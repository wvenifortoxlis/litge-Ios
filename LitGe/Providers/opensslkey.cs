// Type: PrivateURL.opensslkey
// Assembly: Literacy, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// Assembly location: C:\Users\dpopiashvili.DEA\AppData\Local\Apps\2.0\AR6KKHV6.Y9B\V18404WV.JZX\lite..tion_77b3901fa309920b_0001.0000_0e37e62126a8e816\Literacy.dll

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace LitGe.Providers;

public class opensslkey
{
    private static bool verbose = false;
    private const string pemprivheader = "-----BEGIN RSA PRIVATE KEY-----";
    private const string pemprivfooter = "-----END RSA PRIVATE KEY-----";
    private const string pempubheader = "-----BEGIN PUBLIC KEY-----";
    private const string pempubfooter = "-----END PUBLIC KEY-----";
    private const string pemp8header = "-----BEGIN PRIVATE KEY-----";
    private const string pemp8footer = "-----END PRIVATE KEY-----";
    private const string pemp8encheader = "-----BEGIN ENCRYPTED PRIVATE KEY-----";
    private const string pemp8encfooter = "-----END ENCRYPTED PRIVATE KEY-----";

    static opensslkey() { }

    public static void DecodePEMKey(string pemstr)
    {
        if (
            pemstr.StartsWith("-----BEGIN PUBLIC KEY-----")
            && pemstr.EndsWith("-----END PUBLIC KEY-----")
        )
        {
            Debug.WriteLine("Trying to decode and parse a PEM public key ..");
            byte[] numArray = opensslkey.DecodeOpenSSLPublicKey(pemstr);
            if (numArray == null)
                return;
            if (opensslkey.verbose)
                opensslkey.showBytes("\nRSA public key", numArray);
            RSACryptoServiceProvider cryptoServiceProvider = opensslkey.DecodeX509PublicKey(
                numArray
            );
            Debug.WriteLine("\nCreated an RSACryptoServiceProvider instance\n");
            string str = cryptoServiceProvider.ToXmlString(false);
            Debug.WriteLine(
                "\nXML RSA public key:  {0} bits\n{1}\n",
                (object)cryptoServiceProvider.KeySize,
                (object)str
            );
        }
        else if (
            pemstr.StartsWith("-----BEGIN RSA PRIVATE KEY-----")
            && pemstr.EndsWith("-----END RSA PRIVATE KEY-----")
        )
        {
            Debug.WriteLine("Trying to decrypt and parse a PEM private key ..");
            byte[] numArray = opensslkey.DecodeOpenSSLPrivateKey(pemstr);
            if (numArray == null)
                return;
            if (opensslkey.verbose)
                opensslkey.showBytes("\nRSA private key", numArray);
            RSACryptoServiceProvider rsa = opensslkey.DecodeRSAPrivateKey(numArray);
            Debug.WriteLine("\nCreated an RSACryptoServiceProvider instance\n");
            string str = rsa.ToXmlString(true);
            Debug.WriteLine(
                "\nXML RSA private key:  {0} bits\n{1}\n",
                (object)rsa.KeySize,
                (object)str
            );
            opensslkey.ProcessRSA(rsa);
        }
        else if (
            pemstr.StartsWith("-----BEGIN PRIVATE KEY-----")
            && pemstr.EndsWith("-----END PRIVATE KEY-----")
        )
        {
            Debug.WriteLine("Trying to decode and parse as PEM PKCS #8 PrivateKeyInfo ..");
            byte[] numArray = opensslkey.DecodePkcs8PrivateKey(pemstr);
            if (numArray == null)
                return;
            if (opensslkey.verbose)
                opensslkey.showBytes("\nPKCS #8 PrivateKeyInfo", numArray);
            RSACryptoServiceProvider rsa = opensslkey.DecodePrivateKeyInfo(numArray);
            if (rsa != null)
            {
                Debug.WriteLine("\nCreated an RSACryptoServiceProvider instance\n");
                string str = rsa.ToXmlString(true);
                Debug.WriteLine(
                    "\nXML RSA private key:  {0} bits\n{1}\n",
                    (object)rsa.KeySize,
                    (object)str
                );
                opensslkey.ProcessRSA(rsa);
            }
            else
                Debug.WriteLine("\nFailed to create an RSACryptoServiceProvider");
        }
        else if (
            pemstr.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----")
            && pemstr.EndsWith("-----END ENCRYPTED PRIVATE KEY-----")
        )
        {
            Debug.WriteLine("Trying to decode and parse as PEM PKCS #8 EncryptedPrivateKeyInfo ..");
            byte[] numArray = opensslkey.DecodePkcs8EncPrivateKey(pemstr);
            if (numArray == null)
                return;
            if (opensslkey.verbose)
                opensslkey.showBytes("\nPKCS #8 EncryptedPrivateKeyInfo", numArray);
            RSACryptoServiceProvider rsa = opensslkey.DecodeEncryptedPrivateKeyInfo(numArray);
            if (rsa != null)
            {
                Debug.WriteLine("\nCreated an RSACryptoServiceProvider instance\n");
                string str = rsa.ToXmlString(true);
                Debug.WriteLine(
                    "\nXML RSA private key:  {0} bits\n{1}\n",
                    (object)rsa.KeySize,
                    (object)str
                );
                opensslkey.ProcessRSA(rsa);
            }
            else
                Debug.WriteLine("\nFailed to create an RSACryptoServiceProvider");
        }
        else
            Debug.WriteLine("Not a PEM public, private key or a PKCS #8");
    }

    public static void DecodeDERKey(string filename)
    {
        byte[] fileBytes = opensslkey.GetFileBytes(filename);
        if (fileBytes == null)
            return;
        RSACryptoServiceProvider cryptoServiceProvider = opensslkey.DecodeX509PublicKey(fileBytes);
        if (cryptoServiceProvider != null)
        {
            Debug.WriteLine("\nA valid SubjectPublicKeyInfo\n");
            Debug.WriteLine("\nCreated an RSACryptoServiceProvider instance\n");
            string str = cryptoServiceProvider.ToXmlString(false);
            Debug.WriteLine(
                "\nXML RSA public key:  {0} bits\n{1}\n",
                (object)cryptoServiceProvider.KeySize,
                (object)str
            );
        }
        else
        {
            RSACryptoServiceProvider rsa1 = opensslkey.DecodeRSAPrivateKey(fileBytes);
            if (rsa1 != null)
            {
                Debug.WriteLine("\nA valid RSAPrivateKey\n");
                Debug.WriteLine("\nCreated an RSACryptoServiceProvider instance\n");
                string str = rsa1.ToXmlString(true);
                Debug.WriteLine(
                    "\nXML RSA private key:  {0} bits\n{1}\n",
                    (object)rsa1.KeySize,
                    (object)str
                );
                opensslkey.ProcessRSA(rsa1);
            }
            else
            {
                RSACryptoServiceProvider rsa2 = opensslkey.DecodePrivateKeyInfo(fileBytes);
                if (rsa2 != null)
                {
                    Debug.WriteLine("\nA valid PKCS #8 PrivateKeyInfo\n");
                    Debug.WriteLine("\nCreated an RSACryptoServiceProvider instance\n");
                    string str = rsa2.ToXmlString(true);
                    Debug.WriteLine(
                        "\nXML RSA private key:  {0} bits\n{1}\n",
                        (object)rsa2.KeySize,
                        (object)str
                    );
                    opensslkey.ProcessRSA(rsa2);
                }
                else
                {
                    RSACryptoServiceProvider rsa3 = opensslkey.DecodeEncryptedPrivateKeyInfo(
                        fileBytes
                    );
                    if (rsa3 != null)
                    {
                        Debug.WriteLine("\nA valid PKCS #8 EncryptedPrivateKeyInfo\n");
                        Debug.WriteLine("\nCreated an RSACryptoServiceProvider instance\n");
                        string str = rsa3.ToXmlString(true);
                        Debug.WriteLine(
                            "\nXML RSA private key:  {0} bits\n{1}\n",
                            (object)rsa3.KeySize,
                            (object)str
                        );
                        opensslkey.ProcessRSA(rsa3);
                    }
                    else
                        Debug.WriteLine("Not a binary DER public, private or PKCS #8 key");
                }
            }
        }
    }

    public static void ProcessRSA(RSACryptoServiceProvider rsa)
    {
        if (opensslkey.verbose)
            opensslkey.showRSAProps(rsa);
        Debug.Write("\n\nExport RSA private key to PKCS #12 file?  (Y or N) ");
        //string str = Debug.Assert().ToUpper();
        //if (!(str == "Y") && !(str == "YES"))
        //return;
        //opensslkey.RSAtoPKCS12(rsa);
    }

    //public static void RSAtoPKCS12(RSACryptoServiceProvider rsa)
    //{
    //    CspKeyContainerInfo keyContainerInfo = rsa.CspKeyContainerInfo;
    //    string keyContainerName = keyContainerInfo.KeyContainerName;
    //    uint KEYSPEC = (uint)keyContainerInfo.KeyNumber;
    //    string providerName = keyContainerInfo.ProviderName;
    //    uint cspflags = 0U;
    //    string outfile = keyContainerName + ".p12";
    //    byte[] pkcs12 = opensslkey.GetPkcs12((RSA)rsa, keyContainerName, providerName, KEYSPEC, cspflags);
    //    if (pkcs12 != null && opensslkey.verbose)
    //        opensslkey.showBytes("\npkcs #12", pkcs12);
    //    if (pkcs12 != null)
    //    {
    //        opensslkey.PutFileBytes(outfile, pkcs12, pkcs12.Length);
    //        Debug.WriteLine("\nWrote pkc #12 file '{0}'\n", (object)outfile);
    //    }
    //    else
    //        Debug.WriteLine("\nProblem getting pkcs#12");
    //}

    public static byte[] DecodePkcs8PrivateKey(string instr)
    {
        string str = instr.Trim();
        if (
            !str.StartsWith("-----BEGIN PRIVATE KEY-----")
            || !str.EndsWith("-----END PRIVATE KEY-----")
        )
            return (byte[])null;
        StringBuilder stringBuilder = new StringBuilder(str);
        stringBuilder.Replace("-----BEGIN PRIVATE KEY-----", "");
        stringBuilder.Replace("-----END PRIVATE KEY-----", "");
        string s = ((object)stringBuilder).ToString().Trim();
        byte[] numArray;
        try
        {
            numArray = Convert.FromBase64String(s);
        }
        catch (FormatException ex)
        {
            return (byte[])null;
        }
        return numArray;
    }

    public static RSACryptoServiceProvider DecodePrivateKeyInfo(byte[] pkcs8)
    {
        byte[] b = new byte[15]
        {
            (byte)48,
            (byte)13,
            (byte)6,
            (byte)9,
            (byte)42,
            (byte)134,
            (byte)72,
            (byte)134,
            (byte)247,
            (byte)13,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)5,
            (byte)0,
        };
        byte[] numArray = new byte[15];
        MemoryStream memoryStream = new MemoryStream(pkcs8);
        int num1 = (int)memoryStream.Length;
        BinaryReader binaryReader = new BinaryReader((Stream)memoryStream);
        try
        {
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num2 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num3 = (int)binaryReader.ReadInt16();
                    break;
                default:
                    return (RSACryptoServiceProvider)null;
            }
            if (
                (int)binaryReader.ReadByte() != 2
                || (int)binaryReader.ReadUInt16() != 1
                || (
                    !opensslkey.CompareBytearrays(binaryReader.ReadBytes(15), b)
                    || (int)binaryReader.ReadByte() != 4
                )
            )
                return (RSACryptoServiceProvider)null;
            switch (binaryReader.ReadByte())
            {
                case (byte)129:
                    int num4 = (int)binaryReader.ReadByte();
                    break;
                case (byte)130:
                    int num5 = (int)binaryReader.ReadUInt16();
                    break;
            }
            return opensslkey.DecodeRSAPrivateKey(
                binaryReader.ReadBytes((int)((long)num1 - memoryStream.Position))
            );
        }
        catch (Exception ex)
        {
            return (RSACryptoServiceProvider)null;
        }
        finally
        {
            binaryReader.Dispose();
        }
    }

    public static byte[] DecodePkcs8EncPrivateKey(string instr)
    {
        string str = instr.Trim();
        if (
            !str.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----")
            || !str.EndsWith("-----END ENCRYPTED PRIVATE KEY-----")
        )
            return (byte[])null;
        StringBuilder stringBuilder = new StringBuilder(str);
        stringBuilder.Replace("-----BEGIN ENCRYPTED PRIVATE KEY-----", "");
        stringBuilder.Replace("-----END ENCRYPTED PRIVATE KEY-----", "");
        string s = ((object)stringBuilder).ToString().Trim();
        byte[] numArray;
        try
        {
            numArray = Convert.FromBase64String(s);
        }
        catch (FormatException ex)
        {
            return (byte[])null;
        }
        return numArray;
    }

    public static RSACryptoServiceProvider DecodeEncryptedPrivateKeyInfo(byte[] encpkcs8)
    {
        byte[] b1 = new byte[11]
        {
            (byte)6,
            (byte)9,
            (byte)42,
            (byte)134,
            (byte)72,
            (byte)134,
            (byte)247,
            (byte)13,
            (byte)1,
            (byte)5,
            (byte)13,
        };
        byte[] b2 = new byte[11]
        {
            (byte)6,
            (byte)9,
            (byte)42,
            (byte)134,
            (byte)72,
            (byte)134,
            (byte)247,
            (byte)13,
            (byte)1,
            (byte)5,
            (byte)12,
        };
        byte[] b3 = new byte[10]
        {
            (byte)6,
            (byte)8,
            (byte)42,
            (byte)134,
            (byte)72,
            (byte)134,
            (byte)247,
            (byte)13,
            (byte)3,
            (byte)7,
        };
        byte[] numArray1 = new byte[10];
        byte[] numArray2 = new byte[11];
        MemoryStream memoryStream = new MemoryStream(encpkcs8);
        int num1 = (int)memoryStream.Length;
        BinaryReader binaryReader = new BinaryReader((Stream)memoryStream);
        try
        {
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num2 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num3 = (int)binaryReader.ReadInt16();
                    break;
                default:
                    return (RSACryptoServiceProvider)null;
            }
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num4 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num5 = (int)binaryReader.ReadInt16();
                    break;
            }
            if (!opensslkey.CompareBytearrays(binaryReader.ReadBytes(11), b1))
                return (RSACryptoServiceProvider)null;
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num6 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num7 = (int)binaryReader.ReadInt16();
                    break;
            }
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num8 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num9 = (int)binaryReader.ReadInt16();
                    break;
            }
            if (!opensslkey.CompareBytearrays(binaryReader.ReadBytes(11), b2))
                return (RSACryptoServiceProvider)null;
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num10 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num11 = (int)binaryReader.ReadInt16();
                    break;
            }
            if ((int)binaryReader.ReadByte() != 4)
                return (RSACryptoServiceProvider)null;
            int count1 = (int)binaryReader.ReadByte();
            byte[] numArray3 = binaryReader.ReadBytes(count1);
            if (opensslkey.verbose)
                opensslkey.showBytes("Salt for pbkd", numArray3);
            if ((int)binaryReader.ReadByte() != 2)
                return (RSACryptoServiceProvider)null;
            int iterations;
            switch (binaryReader.ReadByte())
            {
                case (byte)1:
                    iterations = (int)binaryReader.ReadByte();
                    break;
                case (byte)2:
                    iterations = 256 * (int)binaryReader.ReadByte() + (int)binaryReader.ReadByte();
                    break;
                default:
                    return (RSACryptoServiceProvider)null;
            }
            if (opensslkey.verbose)
                Debug.WriteLine("PBKD2 iterations {0}", (object)iterations);
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num12 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num13 = (int)binaryReader.ReadInt16();
                    break;
            }
            if (
                !opensslkey.CompareBytearrays(binaryReader.ReadBytes(10), b3)
                || (int)binaryReader.ReadByte() != 4
            )
                return (RSACryptoServiceProvider)null;
            int count2 = (int)binaryReader.ReadByte();
            byte[] numArray4 = binaryReader.ReadBytes(count2);
            if (opensslkey.verbose)
                opensslkey.showBytes("IV for des-EDE3-CBC", numArray4);
            if ((int)binaryReader.ReadByte() != 4)
                return (RSACryptoServiceProvider)null;
            byte num14 = binaryReader.ReadByte();
            int count3;
            switch (num14)
            {
                case (byte)129:
                    count3 = (int)binaryReader.ReadByte();
                    break;
                case (byte)130:
                    count3 = 256 * (int)binaryReader.ReadByte() + (int)binaryReader.ReadByte();
                    break;
                default:
                    count3 = (int)num14;
                    break;
            }
            byte[] edata = binaryReader.ReadBytes(count3);
            //            var secPswd = opensslkey.GetSecPswd("Enter password for Encrypted PKCS #8 ==>");
            //            byte[] pkcs8 = opensslkey.DecryptPBDK2(edata, numArray3, numArray4, (string)secPswd, iterations);
            byte[] pkcs8 = opensslkey.DecryptPBDK2(
                edata,
                numArray3,
                numArray4,
                "OneTwoThree",
                iterations
            );
            if (pkcs8 == null)
                return (RSACryptoServiceProvider)null;
            else
                return opensslkey.DecodePrivateKeyInfo(pkcs8);
        }
        catch (Exception ex)
        {
            return (RSACryptoServiceProvider)null;
        }
        finally
        {
            binaryReader.Dispose();
        }
    }

    public static byte[] DecryptPBDK2(
        byte[] edata,
        byte[] salt,
        byte[] IV,
        string secpswd,
        int iterations
    )
    {
        IntPtr num1 = IntPtr.Zero;
        byte[] numArray = new byte[secpswd.Length];
        IntPtr num2 = Marshal.StringToHGlobalAnsi(secpswd);
        Marshal.Copy(num2, numArray, 0, numArray.Length);
        Marshal.ZeroFreeGlobalAllocAnsi(num2);
        try
        {
            Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(
                numArray,
                salt,
                iterations
            );
            TripleDES tripleDes = TripleDES.Create();
            tripleDes.Key = rfc2898DeriveBytes.GetBytes(24);
            tripleDes.IV = IV;
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(
                (Stream)memoryStream,
                tripleDes.CreateDecryptor(),
                CryptoStreamMode.Write
            );
            cryptoStream.Write(edata, 0, edata.Length);
            cryptoStream.Flush();
            cryptoStream.Dispose();
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Problem decrypting: {0}", (object)ex.Message);
            return (byte[])null;
        }
    }

    public static byte[] DecodeOpenSSLPublicKey(string instr)
    {
        string str = instr.Trim();
        if (
            !str.StartsWith("-----BEGIN PUBLIC KEY-----")
            || !str.EndsWith("-----END PUBLIC KEY-----")
        )
            return (byte[])null;
        StringBuilder stringBuilder = new StringBuilder(str);
        stringBuilder.Replace("-----BEGIN PUBLIC KEY-----", "");
        stringBuilder.Replace("-----END PUBLIC KEY-----", "");
        string s = ((object)stringBuilder).ToString().Trim();
        byte[] numArray;
        try
        {
            numArray = Convert.FromBase64String(s);
        }
        catch (FormatException ex)
        {
            return (byte[])null;
        }
        return numArray;
    }

    public static RSACryptoServiceProvider DecodeX509PublicKey(byte[] x509key)
    {
        byte[] b = new byte[15]
        {
            (byte)48,
            (byte)13,
            (byte)6,
            (byte)9,
            (byte)42,
            (byte)134,
            (byte)72,
            (byte)134,
            (byte)247,
            (byte)13,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)5,
            (byte)0,
        };
        byte[] numArray = new byte[15];
        BinaryReader binaryReader = new BinaryReader((Stream)new MemoryStream(x509key));
        try
        {
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num1 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num2 = (int)binaryReader.ReadInt16();
                    break;
                default:
                    return (RSACryptoServiceProvider)null;
            }
            if (!opensslkey.CompareBytearrays(binaryReader.ReadBytes(15), b))
                return (RSACryptoServiceProvider)null;
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33027:
                    int num3 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33283:
                    int num4 = (int)binaryReader.ReadInt16();
                    break;
                default:
                    return (RSACryptoServiceProvider)null;
            }
            if ((int)binaryReader.ReadByte() != 0)
                return (RSACryptoServiceProvider)null;
            switch (binaryReader.ReadUInt16())
            {
                case (ushort)33072:
                    int num5 = (int)binaryReader.ReadByte();
                    break;
                case (ushort)33328:
                    int num6 = (int)binaryReader.ReadInt16();
                    break;
                default:
                    return (RSACryptoServiceProvider)null;
            }
            ushort num7 = binaryReader.ReadUInt16();
            byte num8 = (byte)0;
            byte num9;
            if ((int)num7 == 33026)
            {
                num9 = binaryReader.ReadByte();
            }
            else
            {
                if ((int)num7 != 33282)
                    return (RSACryptoServiceProvider)null;
                num8 = binaryReader.ReadByte();
                num9 = binaryReader.ReadByte();
            }
            int count1 = BitConverter.ToInt32(new byte[4] { num9, num8, (byte)0, (byte)0 }, 0);
            byte num10 = binaryReader.ReadByte();
            binaryReader.BaseStream.Seek(-1L, SeekOrigin.Current);
            if ((int)num10 == 0)
            {
                int num11 = (int)binaryReader.ReadByte();
                --count1;
            }
            byte[] data1 = binaryReader.ReadBytes(count1);
            if ((int)binaryReader.ReadByte() != 2)
                return (RSACryptoServiceProvider)null;
            int count2 = (int)binaryReader.ReadByte();
            byte[] data2 = binaryReader.ReadBytes(count2);
            opensslkey.showBytes("\nExponent", data2);
            opensslkey.showBytes("\nModulus", data1);
            RSACryptoServiceProvider cryptoServiceProvider = new RSACryptoServiceProvider();
            cryptoServiceProvider.ImportParameters(
                new RSAParameters() { Modulus = data1, Exponent = data2 }
            );
            return cryptoServiceProvider;
        }
        catch (Exception ex)
        {
            return (RSACryptoServiceProvider)null;
        }
        finally
        {
            binaryReader.Dispose();
        }
    }

    public static RSACryptoServiceProvider DecodeRSAPrivateKey(byte[] privkey)
    {
        BinaryReader binr = new BinaryReader((Stream)new MemoryStream(privkey));
        try
        {
            switch (binr.ReadUInt16())
            {
                case (ushort)33072:
                    int num1 = (int)binr.ReadByte();
                    break;
                case (ushort)33328:
                    int num2 = (int)binr.ReadInt16();
                    break;
                default:
                    return (RSACryptoServiceProvider)null;
            }
            if ((int)binr.ReadUInt16() != 258 || (int)binr.ReadByte() != 0)
                return (RSACryptoServiceProvider)null;
            int integerSize1 = opensslkey.GetIntegerSize(binr);
            byte[] data1 = binr.ReadBytes(integerSize1);
            int integerSize2 = opensslkey.GetIntegerSize(binr);
            byte[] data2 = binr.ReadBytes(integerSize2);
            int integerSize3 = opensslkey.GetIntegerSize(binr);
            byte[] data3 = binr.ReadBytes(integerSize3);
            int integerSize4 = opensslkey.GetIntegerSize(binr);
            byte[] data4 = binr.ReadBytes(integerSize4);
            int integerSize5 = opensslkey.GetIntegerSize(binr);
            byte[] data5 = binr.ReadBytes(integerSize5);
            int integerSize6 = opensslkey.GetIntegerSize(binr);
            byte[] data6 = binr.ReadBytes(integerSize6);
            int integerSize7 = opensslkey.GetIntegerSize(binr);
            byte[] data7 = binr.ReadBytes(integerSize7);
            int integerSize8 = opensslkey.GetIntegerSize(binr);
            byte[] data8 = binr.ReadBytes(integerSize8);
            Debug.WriteLine("showing components ..");
            if (opensslkey.verbose)
            {
                opensslkey.showBytes("\nModulus", data1);
                opensslkey.showBytes("\nExponent", data2);
                opensslkey.showBytes("\nD", data3);
                opensslkey.showBytes("\nP", data4);
                opensslkey.showBytes("\nQ", data5);
                opensslkey.showBytes("\nDP", data6);
                opensslkey.showBytes("\nDQ", data7);
                opensslkey.showBytes("\nIQ", data8);
            }
            RSACryptoServiceProvider cryptoServiceProvider = new RSACryptoServiceProvider();
            cryptoServiceProvider.ImportParameters(
                new RSAParameters()
                {
                    Modulus = data1,
                    Exponent = data2,
                    D = data3,
                    P = data4,
                    Q = data5,
                    DP = data6,
                    DQ = data7,
                    InverseQ = data8,
                }
            );
            return cryptoServiceProvider;
        }
        catch (Exception ex)
        {
            return (RSACryptoServiceProvider)null;
        }
        finally
        {
            binr.Dispose();
        }
    }

    private static int GetIntegerSize(BinaryReader binr)
    {
        if ((int)binr.ReadByte() != 2)
            return 0;
        byte num1 = binr.ReadByte();
        int num2;
        switch (num1)
        {
            case (byte)129:
                num2 = (int)binr.ReadByte();
                break;
            case (byte)130:
                byte num3 = binr.ReadByte();
                num2 = BitConverter.ToInt32(
                    new byte[4] { binr.ReadByte(), num3, (byte)0, (byte)0 },
                    0
                );
                break;
            default:
                num2 = (int)num1;
                break;
        }
        while ((int)binr.ReadByte() == 0)
            --num2;
        binr.BaseStream.Seek(-1L, SeekOrigin.Current);
        return num2;
    }

    public static byte[] DecodeOpenSSLPrivateKey(string instr)
    {
        string str1 = instr.Trim();
        if (str1.EndsWith("-----END RSA PRIVA"))
            str1 = str1.Replace("-----END RSA PRIVA", "-----END RSA PRIVATE KEY-----");
        if (
            !str1.StartsWith("-----BEGIN RSA PRIVATE KEY-----")
            || !str1.EndsWith("-----END RSA PRIVATE KEY-----")
        )
            return (byte[])null;
        StringBuilder stringBuilder = new StringBuilder(str1);
        stringBuilder.Replace("-----BEGIN RSA PRIVATE KEY-----", "");
        stringBuilder.Replace("-----END RSA PRIVATE KEY-----", "");
        string s1 = ((object)stringBuilder).ToString().Trim();
        try
        {
            return Convert.FromBase64String(s1);
        }
        catch (FormatException ex) { }
        StringReader stringReader = new StringReader(s1);
        if (!stringReader.ReadLine().StartsWith("Proc-Type: 4,ENCRYPTED"))
            return (byte[])null;
        string str2 = stringReader.ReadLine();
        if (!str2.StartsWith("DEK-Info: DES-EDE3-CBC,"))
            return (byte[])null;
        string str3 = str2.Substring(str2.IndexOf(",") + 1).Trim();
        byte[] numArray1 = new byte[str3.Length / 2];
        for (int index = 0; index < numArray1.Length; ++index)
            numArray1[index] = Convert.ToByte(str3.Substring(index * 2, 2), 16);
        if (!(stringReader.ReadLine() == ""))
            return (byte[])null;
        string s2 = stringReader.ReadToEnd();
        byte[] cipherData;
        try
        {
            cipherData = Convert.FromBase64String(s2);
        }
        catch (FormatException ex)
        {
            return (byte[])null;
        }
        //        var secPswd = opensslkey.GetSecPswd("Enter password to derive 3DES key==>");
        //        byte[] openSsL3deskey = opensslkey.GetOpenSSL3deskey(numArray1, (string) secPswd, 1, 2);
        byte[] openSsL3deskey = opensslkey.GetOpenSSL3deskey(numArray1, "OneTwoThree", 1, 2);
        if (openSsL3deskey == null)
            return (byte[])null;
        byte[] numArray2 = opensslkey.DecryptKey(cipherData, openSsL3deskey, numArray1);
        if (numArray2 != null)
            return numArray2;
        Debug.WriteLine("Failed to decrypt RSA private key; probably wrong password.");
        return (byte[])null;
    }

    public static byte[] DecryptKey(byte[] cipherData, byte[] desKey, byte[] IV)
    {
        MemoryStream memoryStream = new MemoryStream();
        TripleDES tripleDes = TripleDES.Create();
        tripleDes.Key = desKey;
        tripleDes.IV = IV;
        try
        {
            CryptoStream cryptoStream = new CryptoStream(
                (Stream)memoryStream,
                tripleDes.CreateDecryptor(),
                CryptoStreamMode.Write
            );
            cryptoStream.Write(cipherData, 0, cipherData.Length);
            cryptoStream.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return (byte[])null;
        }
        return memoryStream.ToArray();
    }

    private static byte[] GetOpenSSL3deskey(byte[] salt, string secpswd, int count, int miter)
    {
        IntPtr num1 = IntPtr.Zero;
        int num2 = 16;
        byte[] numArray1 = new byte[num2 * miter];
        byte[] destination = new byte[secpswd.Length];
        IntPtr num3 = Marshal.StringToHGlobalAnsi(secpswd);
        Marshal.Copy(num3, destination, 0, destination.Length);
        Marshal.ZeroFreeGlobalAllocAnsi(num3);
        byte[] numArray2 = new byte[destination.Length + salt.Length];
        Array.Copy((Array)destination, (Array)numArray2, destination.Length);
        Array.Copy((Array)salt, 0, (Array)numArray2, destination.Length, salt.Length);
        MD5 md5 = (MD5)new MD5CryptoServiceProvider();
        byte[] buffer = (byte[])null;
        byte[] numArray3 = new byte[num2 + numArray2.Length];
        for (int index1 = 0; index1 < miter; ++index1)
        {
            if (index1 == 0)
            {
                buffer = numArray2;
            }
            else
            {
                Array.Copy((Array)buffer, (Array)numArray3, buffer.Length);
                Array.Copy((Array)numArray2, 0, (Array)numArray3, buffer.Length, numArray2.Length);
                buffer = numArray3;
            }
            for (int index2 = 0; index2 < count; ++index2)
                buffer = md5.ComputeHash(buffer);
            Array.Copy((Array)buffer, 0, (Array)numArray1, index1 * num2, buffer.Length);
        }
        byte[] numArray4 = new byte[24];
        Array.Copy((Array)numArray1, (Array)numArray4, numArray4.Length);
        Array.Clear((Array)destination, 0, destination.Length);
        Array.Clear((Array)numArray2, 0, numArray2.Length);
        Array.Clear((Array)buffer, 0, buffer.Length);
        Array.Clear((Array)numArray3, 0, numArray3.Length);
        Array.Clear((Array)numArray1, 0, numArray1.Length);
        return numArray4;
    }

    //private static byte[] GetPkcs12(RSA rsa, string keycontainer, string cspprovider, uint KEYSPEC, uint cspflags)
    //{
    //    IntPtr num = IntPtr.Zero;
    //    string DN = "CN=Opensslkey Unsigned Certificate";
    //    IntPtr unsignedCertCntxt = opensslkey.CreateUnsignedCertCntxt(keycontainer, cspprovider, KEYSPEC, cspflags, DN);
    //    if (unsignedCertCntxt == IntPtr.Zero)
    //    {
    //        Debug.WriteLine("Couldn't create an unsigned-cert\n");
    //        return (byte[])null;
    //    }
    //    else
    //    {
    //        byte[] numArray;
    //        try
    //        {
    //            numArray = new X509Certificate(unsignedCertCntxt).Export(X509ContentType.Pfx, opensslkey.GetSecPswd("Set PFX Password ==>"));
    //        }
    //        catch (Exception ex)
    //        {
    //            Debug.WriteLine("BAD RESULT" + ex.Message);
    //            numArray = (byte[])null;
    //        }
    //        rsa.Clear();
    //        if (unsignedCertCntxt != IntPtr.Zero)
    //            Win32.CertFreeCertificateContext(unsignedCertCntxt);
    //        return numArray;
    //    }
    //}

    //private static IntPtr CreateUnsignedCertCntxt(string keycontainer, string provider, uint KEYSPEC, uint cspflags, string DN)
    //{
    //    IntPtr num = IntPtr.Zero;
    //    byte[] numArray = (byte[])null;
    //    uint pcbEncoded = 0U;
    //    if (provider != "Microsoft Base Cryptographic Provider v1.0" && provider != "Microsoft Strong Cryptographic Provider" && provider != "Microsoft Enhanced Cryptographic Provider v1.0" || keycontainer == "" || (int)KEYSPEC != 2 && (int)KEYSPEC != 1 || ((int)cspflags != 0 && (int)cspflags != 32 || DN == ""))
    //        return IntPtr.Zero;
    //    if (Win32.CertStrToName(1U, DN, 3U, IntPtr.Zero, (byte[])null, ref pcbEncoded, IntPtr.Zero))
    //    {
    //        numArray = new byte[(int)pcbEncoded];
    //        Win32.CertStrToName(1U, DN, 3U, IntPtr.Zero, numArray, ref pcbEncoded, IntPtr.Zero);
    //    }
    //    CERT_NAME_BLOB pSubjectIssuerBlob = new CERT_NAME_BLOB();
    //    pSubjectIssuerBlob.pbData = Marshal.AllocHGlobal(numArray.Length);
    //    Marshal.Copy(numArray, 0, pSubjectIssuerBlob.pbData, numArray.Length);
    //    pSubjectIssuerBlob.cbData = numArray.Length;
    //    var inf = new PrivateURL.CRYPT_KEY_PROV_INFO();
    //    IntPtr selfSignCertificate = Win32.CertCreateSelfSignCertificate(IntPtr.Zero, ref pSubjectIssuerBlob, 1U, ref inf)
    //    {
    //        pwszContainerName = keycontainer,
    //        pwszProvName = provider,
    //        dwProvType = 1U,
    //        dwFlags = cspflags,
    //        cProvParam = 0U,
    //        rgProvParam = IntPtr.Zero,
    //        dwKeySpec = KEYSPEC
    //    }, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    //    if (selfSignCertificate == IntPtr.Zero)
    //        opensslkey.showWin32Error(Marshal.GetLastWin32Error());
    //    Marshal.FreeHGlobal(pSubjectIssuerBlob.pbData);
    //    return selfSignCertificate;
    //}



    private static bool CompareBytearrays(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        int index = 0;
        foreach (int num in a)
        {
            if (num != (int)b[index])
                return false;
            ++index;
        }
        return true;
    }

    private static void showRSAProps(RSACryptoServiceProvider rsa)
    {
        Debug.WriteLine("RSA CSP key information:");
        /*
    CspKeyContainerInfo keyContainerInfo = rsa.CspKeyContainerInfo;
    Debug.WriteLine("Accessible property: " + (object)(keyContainerInfo.Accessible ? 1 : 0));
    Debug.WriteLine("Exportable property: " + (object)(keyContainerInfo.Exportable ? 1 : 0));
    Debug.WriteLine("HardwareDevice property: " + (object)(keyContainerInfo.HardwareDevice ? 1 : 0));
    Debug.WriteLine("KeyContainerName property: " + keyContainerInfo.KeyContainerName);
    Debug.WriteLine("KeyNumber property: " + ((object)keyContainerInfo.KeyNumber).ToString());
    Debug.WriteLine("MachineKeyStore property: " + (object)(keyContainerInfo.MachineKeyStore ? 1 : 0));
    Debug.WriteLine("Protected property: " + (object)(keyContainerInfo.Protected ? 1 : 0));
    Debug.WriteLine("ProviderName property: " + keyContainerInfo.ProviderName);
    Debug.WriteLine("ProviderType property: " + (object)keyContainerInfo.ProviderType);
    Debug.WriteLine("RandomlyGenerated property: " + (object)(keyContainerInfo.RandomlyGenerated ? 1 : 0));
    Debug.WriteLine("Removable property: " + (object)(keyContainerInfo.Removable ? 1 : 0));
    Debug.WriteLine("UniqueKeyContainerName property: " + keyContainerInfo.UniqueKeyContainerName);
    */
    }

    private static void showBytes(string info, byte[] data)
    {
        Debug.WriteLine("\n\n");
    }

    private static byte[] GetFileBytes(string filename)
    {
        if (!File.Exists(filename))
            return (byte[])null;
        Stream stream = (Stream)new FileStream(filename, FileMode.Open);
        int count = (int)stream.Length;
        byte[] buffer = new byte[count];
        stream.Seek(0L, SeekOrigin.Begin);
        stream.Read(buffer, 0, count);
        stream.Dispose();
        return buffer;
    }

    private static void PutFileBytes(string outfile, byte[] data, int bytes)
    {
        FileStream fileStream = (FileStream)null;
        if (bytes > data.Length)
        {
            Debug.WriteLine("Too many bytes");
        }
        else
        {
            try
            {
                fileStream = new FileStream(outfile, FileMode.Create);
                fileStream.Write(data, 0, bytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                fileStream.Dispose();
            }
        }
    }
}
