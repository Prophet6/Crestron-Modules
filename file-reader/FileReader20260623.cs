using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp; // Crestron-specific namespace
using Crestron.SimplSharp.CrestronIO; // Crestron IO for FileStream
using Crestron.SimplSharp.CrestronXml; // Crestron XML handling
using Newtonsoft.Json; // SimplSharpNewtonsoft.dll must be referenced
using Newtonsoft.Json.Linq;
using Crestron.SimplSharp.Cryptography; // For encryption
using Crestron.SimplSharp.CrestronIO.Compression; // For GZipStream

namespace CrestronFileHandler
{
    public class SignalData
    {
        public ushort[] digitals { get; set; }
        public ushort[] analogs { get; set; }
        public string[] serials { get; set; }
        public uint crc32 { get; set; } // For JSON
    }

    public class FileDataHandler
    {
        public string FilePath { get; set; }
        public ushort[] Digitals { get; private set; }
        public ushort[] Analogs { get; private set; }
        public string[] Serials { get; private set; }
        public short DetectedFormat { get; private set; }
        public string Feedback { get; private set; }
        public string EncryptionKey { get; set; }
        public ushort UseEncryption { get; set; }
        public ushort EnableBackupHistory { get; set; }
        public const ushort MaxSignals = 64;

        private readonly string[] _formats = { "Text", "CSV", "XML", "JSON", "INI", "Binary" };
        private const string EncryptionHeader = "ENC:AES:";

        public FileDataHandler()
        {
            Digitals = new ushort[MaxSignals];
            Analogs = new ushort[MaxSignals];
            Serials = new string[MaxSignals];
            for (int i = 0; i < MaxSignals; i++)
            {
                Serials[i] = "";
            }
            DetectedFormat = -1; // Default
            Feedback = ""; // Default
            EncryptionKey = "";
            UseEncryption = 0;
            EnableBackupHistory = 0;
        }

        /// <summary>
        /// Checks if the file is encrypted by looking for the header.
        /// </summary>
        /// <returns>1 if encrypted, 0 otherwise</returns>
        public short IsEncrypted()
        {
            if (string.IsNullOrEmpty(FilePath) || !Crestron.SimplSharp.CrestronIO.File.Exists(FilePath))
            {
                return 0;
            }
            try
            {
                using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] headerBytes = new byte[EncryptionHeader.Length];
                    int bytesRead = fs.Read(headerBytes, 0, headerBytes.Length);
                    if (bytesRead == headerBytes.Length && Encoding.UTF8.GetString(headerBytes, 0, headerBytes.Length) == EncryptionHeader)
                    {
                        return 1;
                    }
                    return 0;
                }
            }
            catch (Exception e)
            {
                Feedback = string.Format("IsEncrypted error: {0}", e.Message);
                return 0;
            }
        }

        /// <summary>
        /// Checks if the backup file exists at FilePath + ".bak".
        /// </summary>
        /// <returns>1 if exists, 0 otherwise or on error</returns>
        public short CheckBackupExists()
        {
            string bakPath = FilePath + ".bak";
            if (string.IsNullOrEmpty(FilePath))
            {
                Feedback = "File path is empty";
                return 0;
            }
            try
            {
                return Crestron.SimplSharp.CrestronIO.File.Exists(bakPath) ? (short)1 : (short)0;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Check backup exists error: {0}", e.Message);
                Feedback = string.Format("Check backup error: {0}", e.Message);
                return 0;
            }
        }

        /// <summary>
        /// Backs up the file to .bak, optionally archiving any existing .bak to a dated .gz compressed file first.
        /// </summary>
        /// <returns>0 on success, -1 on error</returns>
        public short BackupFile()
        {
            string bakPath = FilePath + ".bak";
            if (string.IsNullOrEmpty(FilePath) || !Crestron.SimplSharp.CrestronIO.File.Exists(FilePath))
            {
                Feedback = "No file to backup";
                return -1;
            }
            try
            {
                // If existing .bak and history enabled, archive it to dated .gz
                if (EnableBackupHistory == 1 && Crestron.SimplSharp.CrestronIO.File.Exists(bakPath))
                {
                    DateTime now = DateTime.Now;
                    string datedBak = FilePath + ".bak." + now.ToString("yyyyMMdd") + ".gz";
                    using (FileStream input = new FileStream(bakPath, FileMode.Open, FileAccess.Read))
                    using (FileStream output = new FileStream(datedBak, FileMode.Create, FileAccess.Write))
                    using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress))
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            gzip.Write(buffer, 0, bytesRead);
                        }
                    }
                    // Note: We don't delete the old .bak here; we'll overwrite it below
                }

                Crestron.SimplSharp.CrestronIO.File.Copy(FilePath, bakPath, true);
                Feedback = "Backup created";
                return 0;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Backup error: {0}", e.Message);
                Feedback = string.Format("Backup error: {0}", e.Message);
                return -1;
            }
        }

        /// <summary>
        /// Restores from .bak by swapping with FilePath (undoable by swapping again).
        /// </summary>
        /// <returns>0 on success, -1 on error</returns>
        public short RestoreBackup()
        {
            string bakPath = FilePath + ".bak";
            string tempPath = FilePath + ".tmp";
            if (string.IsNullOrEmpty(FilePath) || !Crestron.SimplSharp.CrestronIO.File.Exists(bakPath))
            {
                Feedback = "No backup to restore";
                return -1;
            }
            try
            {
                // Swap: main -> temp, .bak -> main, temp -> .bak
                if (Crestron.SimplSharp.CrestronIO.File.Exists(FilePath))
                {
                    Crestron.SimplSharp.CrestronIO.File.Move(FilePath, tempPath);
                }
                Crestron.SimplSharp.CrestronIO.File.Move(bakPath, FilePath);
                if (Crestron.SimplSharp.CrestronIO.File.Exists(tempPath))
                {
                    Crestron.SimplSharp.CrestronIO.File.Move(tempPath, bakPath);
                }
                Feedback = "Backup restored (swap)";
                return 0;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Restore error: {0}", e.Message);
                Feedback = string.Format("Restore error: {0}", e.Message);
                // Clean up temp if failed
                if (Crestron.SimplSharp.CrestronIO.File.Exists(tempPath))
                {
                    Crestron.SimplSharp.CrestronIO.File.Delete(tempPath);
                }
                return -1;
            }
        }

        /// <summary>
        /// Checks if the file exists at FilePath.
        /// </summary>
        /// <returns>1 if exists, 0 otherwise or on error</returns>
        public short CheckFileExists()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                Feedback = "File path is empty";
                return 0;
            }
            try
            {
                return Crestron.SimplSharp.CrestronIO.File.Exists(FilePath) ? (short)1 : (short)0;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Check file exists error: {0}", e.Message);
                Feedback = string.Format("Check error: {0}", e.Message);
                return 0;
            }
        }

        /// <summary>
        /// Gets the formatted file size as a string (e.g., "1.23 KB").
        /// </summary>
        /// <returns>Formatted file size string</returns>
        public string GetFileSizeString()
        {
            if (string.IsNullOrEmpty(FilePath) || !Crestron.SimplSharp.CrestronIO.File.Exists(FilePath))
            {
                return "0 bytes";
            }
            try
            {
                using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    long size = fs.Length;
                    if (size < 1024)
                    {
                        return size + " bytes";
                    }
                    else if (size < 1024 * 1024)
                    {
                        return Math.Round((double)size / 1024, 2) + " KB";
                    }
                    else
                    {
                        return Math.Round((double)size / (1024 * 1024), 2) + " MB";
                    }
                }
            }
            catch (Exception e)
            {
                Feedback = string.Format("Size error: {0}", e.Message);
                return "Error";
            }
        }

        /// <summary>
        /// Deletes the file at FilePath.
        /// </summary>
        /// <returns>0 on success, -1 on error</returns>
        public short Delete()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                Feedback = "File path is empty";
                return -1;
            }
            try
            {
                if (Crestron.SimplSharp.CrestronIO.File.Exists(FilePath))
                {
                    Crestron.SimplSharp.CrestronIO.File.Delete(FilePath);
                    Feedback = "File deleted successfully";
                    return 0;
                }
                else
                {
                    Feedback = "File does not exist";
                    return -1;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("File delete error: {0}", e.Message);
                Feedback = string.Format("Delete error: {0}", e.Message);
                return -1;
            }
        }

        /// <summary>
        /// Deletes the backup file at FilePath + ".bak".
        /// </summary>
        /// <returns>0 on success, -1 on error</returns>
        public short DeleteBackup()
        {
            string bakPath = FilePath + ".bak";
            if (string.IsNullOrEmpty(FilePath))
            {
                Feedback = "File path is empty";
                return -1;
            }
            try
            {
                if (Crestron.SimplSharp.CrestronIO.File.Exists(bakPath))
                {
                    Crestron.SimplSharp.CrestronIO.File.Delete(bakPath);
                    Feedback = "Backup deleted successfully";
                    return 0;
                }
                else
                {
                    Feedback = "Backup does not exist";
                    return -1;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Backup delete error: {0}", e.Message);
                Feedback = string.Format("Backup delete error: {0}", e.Message);
                return -1;
            }
        }

        /// <summary>
        /// Detects native text format lines (e.g. "D 5 1") without matching embedded
        /// substrings inside JSON/XML/CSV serial values such as "DSP A output".
        /// </summary>
        private bool LooksLikeTextFormat(string content)
        {
            if (!content.Contains("CRC32: "))
            {
                return false;
            }

            string[] lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();
                if (trimmedLine.Length < 3)
                {
                    continue;
                }

                if (trimmedLine.StartsWith("D ") || trimmedLine.StartsWith("A ") || trimmedLine.StartsWith("S "))
                {
                    string[] parts = trimmedLine.Split(' ');
                    if (parts.Length >= 3)
                    {
                        int index = 0;
                        try
                        {
                            index = int.Parse(parts[1]);
                        }
                        catch
                        {
                            continue;
                        }

                        if (index >= 1 && index <= MaxSignals)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // Custom CRC32 implementation (since Crestron lacks built-in CRC32 in SimplSharp)
        private uint ComputeCRC32(byte[] data)
        {
            uint crcValue = 0xFFFFFFFF;
            uint poly = 0xEDB88320;
            for (int i = 0; i < data.Length; i++)
            {
                crcValue ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    crcValue = (crcValue & 1) != 0 ? (crcValue >> 1) ^ poly : crcValue >> 1;
                }
            }
            return ~crcValue;
        }

        /// <summary>
        /// Encrypts data using AES if enabled.
        /// </summary>
        private byte[] EncryptData(byte[] plainData)
        {
            if (UseEncryption == 0)
            {
                return plainData;
            }

            if (string.IsNullOrEmpty(EncryptionKey))
            {
                Feedback = "Encryption enabled but no key provided";
                throw new Exception(Feedback);
            }

            try
            {
                SymmetricAlgorithm algo = Rijndael.Create();
                algo.KeySize = 128;
                byte[] keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);
                Array.Resize(ref keyBytes, algo.KeySize / 8);
                algo.Key = keyBytes;
                algo.Mode = CipherMode.CBC;
                byte[] iv = new byte[algo.BlockSize / 8];
                RandomNumberGenerator rng = new RNGCryptoServiceProvider();
                rng.GetBytes(iv);
                algo.IV = iv;

                ICryptoTransform encryptor = algo.CreateEncryptor();

                byte[] headerBytes = Encoding.UTF8.GetBytes(EncryptionHeader);

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(headerBytes, 0, headerBytes.Length);
                    ms.Write(iv, 0, iv.Length);
                    byte[] encrypted = encryptor.TransformFinalBlock(plainData, 0, plainData.Length);
                    ms.Write(encrypted, 0, encrypted.Length);
                    return ms.ToArray();
                }
            }
            catch (Exception e)
            {
                Feedback = "Encryption error: " + e.Message;
                throw;
            }
        }

        /// <summary>
        /// Decrypts data using AES if header present.
        /// </summary>
        private byte[] DecryptData(byte[] cipherData)
        {
            byte[] headerBytes = new byte[EncryptionHeader.Length];
            if (cipherData.Length < headerBytes.Length)
            {
                return cipherData;
            }
            Array.Copy(cipherData, 0, headerBytes, 0, headerBytes.Length);
            if (Encoding.UTF8.GetString(headerBytes, 0, headerBytes.Length) != EncryptionHeader)
            {
                return cipherData;
            }

            if (string.IsNullOrEmpty(EncryptionKey))
            {
                Feedback = "Encrypted file but no key provided";
                throw new Exception(Feedback);
            }

            try
            {
                SymmetricAlgorithm algo = Rijndael.Create();
                algo.KeySize = 128;
                byte[] keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);
                Array.Resize(ref keyBytes, algo.KeySize / 8);
                algo.Key = keyBytes;
                algo.Mode = CipherMode.CBC;

                int offset = headerBytes.Length;
                byte[] iv = new byte[algo.BlockSize / 8];
                Array.Copy(cipherData, offset, iv, 0, iv.Length);
                algo.IV = iv;
                offset += iv.Length;

                ICryptoTransform decryptor = algo.CreateDecryptor();

                byte[] decrypted = decryptor.TransformFinalBlock(cipherData, offset, cipherData.Length - offset);

                return decrypted;
            }
            catch (Exception e)
            {
                Feedback = "Decryption error: " + e.Message;
                throw;
            }
        }

        /// <summary>
        /// Writes data to the file in the specified format, with checksum and optional encryption.
        /// </summary>
        public short Write(ushort format)
        {
            if (format < 0 || format > 5)
            {
                Feedback = "Invalid format";
                return -1;
            }

            try
            {
                byte[] dataBytes = null;
                uint crcValue;

                switch (format)
                {
                    case 0: // Text
                        using (MemoryStream dataMs = new MemoryStream())
                        {
                            using (StreamWriter sw = new StreamWriter(dataMs))
                            {
                                for (int i = 0; i < MaxSignals; i++)
                                {
                                    if (Digitals[i] != 0)
                                        sw.WriteLine("D {0} {1}", i + 1, Digitals[i]);
                                    if (Analogs[i] != 0)
                                        sw.WriteLine("A {0} {1}", i + 1, Analogs[i]);
                                    if (!string.IsNullOrEmpty(Serials[i]))
                                        sw.WriteLine("S {0} \"{1}\"", i + 1, Serials[i].Replace("\"", "\"\""));
                                }
                            }
                            byte[] contentBytes = dataMs.ToArray();
                            crcValue = ComputeCRC32(contentBytes);
                            using (MemoryStream finalMs = new MemoryStream())
                            {
                                finalMs.Write(contentBytes, 0, contentBytes.Length);
                                using (StreamWriter fsSw = new StreamWriter(finalMs))
                                {
                                    fsSw.WriteLine("CRC32: {0}", crcValue);
                                }
                                dataBytes = finalMs.ToArray();
                            }
                        }
                        break;

                    case 1: // CSV
                        using (MemoryStream dataMs = new MemoryStream())
                        {
                            using (StreamWriter sw = new StreamWriter(dataMs))
                            {
                                sw.WriteLine("Type,Index,Value");
                                for (int i = 0; i < MaxSignals; i++)
                                {
                                    if (Digitals[i] != 0)
                                        sw.WriteLine("D,{0},{1}", i + 1, Digitals[i]);
                                    if (Analogs[i] != 0)
                                        sw.WriteLine("A,{0},{1}", i + 1, Analogs[i]);
                                    if (!string.IsNullOrEmpty(Serials[i]))
                                        sw.WriteLine("S,{0},\"{1}\"", i + 1, Serials[i].Replace("\"", "\"\""));
                                }
                            }
                            byte[] contentBytes = dataMs.ToArray();
                            crcValue = ComputeCRC32(contentBytes);
                            using (MemoryStream finalMs = new MemoryStream())
                            {
                                finalMs.Write(contentBytes, 0, contentBytes.Length);
                                using (StreamWriter fsSw = new StreamWriter(finalMs))
                                {
                                    fsSw.WriteLine("CRC32: {0}", crcValue);
                                }
                                dataBytes = finalMs.ToArray();
                            }
                        }
                        break;

                    case 2: // XML
                        XmlDocument doc = new XmlDocument();
                        XmlElement dataElem = doc.CreateElement("data");
                        doc.AppendChild(dataElem);

                        XmlElement digitalsElem = doc.CreateElement("digitals");
                        dataElem.AppendChild(digitalsElem);
                        for (int i = 0; i < MaxSignals; i++)
                        {
                            if (Digitals[i] != 0)
                            {
                                XmlElement dElem = doc.CreateElement("d" + (i + 1));
                                dElem.InnerText = Digitals[i].ToString();
                                digitalsElem.AppendChild(dElem);
                            }
                        }

                        XmlElement analogsElem = doc.CreateElement("analogs");
                        dataElem.AppendChild(analogsElem);
                        for (int i = 0; i < MaxSignals; i++)
                        {
                            if (Analogs[i] != 0)
                            {
                                XmlElement aElem = doc.CreateElement("a" + (i + 1));
                                aElem.InnerText = Analogs[i].ToString();
                                analogsElem.AppendChild(aElem);
                            }
                        }

                        XmlElement serialsElem = doc.CreateElement("serials");
                        dataElem.AppendChild(serialsElem);
                        for (int i = 0; i < MaxSignals; i++)
                        {
                            if (!string.IsNullOrEmpty(Serials[i]))
                            {
                                XmlElement sElem = doc.CreateElement("s" + (i + 1));
                                sElem.InnerText = Serials[i];
                                serialsElem.AppendChild(sElem);
                            }
                        }

                        string xmlContent = doc.OuterXml;
                        byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlContent);
                        crcValue = ComputeCRC32(xmlBytes);
                        string xmlWithCrc = xmlContent + string.Format("<!-- CRC32: {0} -->", crcValue);
                        dataBytes = Encoding.UTF8.GetBytes(xmlWithCrc);
                        break;

                    case 3: // JSON — industry-standard object; crc32 is optional metadata
                        {
                            /* Body without crc for optional integrity metadata */
                            string jsonBody = JsonConvert.SerializeObject(new
                            {
                                digitals = Digitals,
                                analogs = Analogs,
                                serials = Serials
                            });
                            crcValue = ComputeCRC32(Encoding.UTF8.GetBytes(jsonBody));
                            SignalData signalData = new SignalData
                            {
                                digitals = Digitals,
                                analogs = Analogs,
                                serials = Serials,
                                crc32 = crcValue
                            };
                            string jsonContent = JsonConvert.SerializeObject(signalData);
                            dataBytes = Encoding.UTF8.GetBytes(jsonContent);
                        }
                        break;

                    case 4: // INI
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("[Digitals]");
                        for (int i = 0; i < MaxSignals; i++)
                        {
                            if (Digitals[i] != 0)
                                sb.AppendLine(string.Format("{0}={1}", i + 1, Digitals[i]));
                        }
                        sb.AppendLine("[Analogs]");
                        for (int i = 0; i < MaxSignals; i++)
                        {
                            if (Analogs[i] != 0)
                                sb.AppendLine(string.Format("{0}={1}", i + 1, Analogs[i]));
                        }
                        sb.AppendLine("[Serials]");
                        for (int i = 0; i < MaxSignals; i++)
                        {
                            if (!string.IsNullOrEmpty(Serials[i]))
                                sb.AppendLine(string.Format("{0}={1}", i + 1, Serials[i].Replace("=", "\\=")));
                        }
                        string iniContent = sb.ToString();
                        byte[] iniBytes = Encoding.UTF8.GetBytes(iniContent);
                        crcValue = ComputeCRC32(iniBytes);
                        string iniWithCrc = iniContent + string.Format("; CRC32: {0}", crcValue);
                        dataBytes = Encoding.UTF8.GetBytes(iniWithCrc);
                        break;

                    case 5: // Binary
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (BinaryWriter bw = new BinaryWriter(ms))
                            {
                                // Digitals
                                int dCount = 0;
                                for (int i = 0; i < MaxSignals; i++) if (Digitals[i] != 0) dCount++;
                                bw.Write(dCount);
                                for (int i = 0; i < MaxSignals; i++)
                                {
                                    if (Digitals[i] != 0)
                                    {
                                        bw.Write((byte)i); // Store index
                                        bw.Write(Digitals[i]);
                                    }
                                }

                                // Analogs
                                int aCount = 0;
                                for (int i = 0; i < MaxSignals; i++) if (Analogs[i] != 0) aCount++;
                                bw.Write(aCount);
                                for (int i = 0; i < MaxSignals; i++)
                                {
                                    if (Analogs[i] != 0)
                                    {
                                        bw.Write((byte)i); // Store index
                                        bw.Write(Analogs[i]);
                                    }
                                }

                                // Serials
                                int sCount = 0;
                                for (int i = 0; i < MaxSignals; i++) if (!string.IsNullOrEmpty(Serials[i])) sCount++;
                                bw.Write(sCount);
                                for (int i = 0; i < MaxSignals; i++)
                                {
                                    if (!string.IsNullOrEmpty(Serials[i]))
                                    {
                                        bw.Write((byte)i); // Store index
                                        byte[] sBytes = Encoding.UTF8.GetBytes(Serials[i]);
                                        bw.Write(sBytes.Length);
                                        bw.Write(sBytes);
                                    }
                                }
                            }
                            byte[] contentBytes = ms.ToArray();
                            crcValue = ComputeCRC32(contentBytes);
                            using (MemoryStream finalMs = new MemoryStream())
                            {
                                finalMs.Write(contentBytes, 0, contentBytes.Length);
                                finalMs.Write(BitConverter.GetBytes(crcValue), 0, 4);
                                dataBytes = finalMs.ToArray();
                            }
                        }
                        break;
                }

                // Encrypt if enabled
                dataBytes = EncryptData(dataBytes);

                // Write to file
                using (FileStream fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(dataBytes, 0, dataBytes.Length);
                }

                Feedback = string.Format("File written in {0} format with checksum{1}", _formats[format], UseEncryption == 1 ? " (encrypted)" : "");
                return 0;
            }
            catch (Exception e)
            {
                Feedback = string.Format("Write error: {0}", e.Message);
                return -1;
            }
        }

        /// <summary>
        /// Reads and auto-detects format from the file, with integrity check and optional decryption.
        /// </summary>
        public short Read()
        {
            if (string.IsNullOrEmpty(FilePath) || !Crestron.SimplSharp.CrestronIO.File.Exists(FilePath))
            {
                Feedback = "File not found";
                DetectedFormat = -1;
                return -1;
            }

            byte[] fullBytes = null;

            try
            {
                using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    fullBytes = new byte[fs.Length];
                    fs.Read(fullBytes, 0, (int)fs.Length);
                }

                // Decrypt if necessary
                fullBytes = DecryptData(fullBytes);

                // NEW: Reset arrays to defaults before parsing to clear any stale data from previous operations
                Array.Clear(Digitals, 0, MaxSignals);
                Array.Clear(Analogs, 0, MaxSignals);
                for (int i = 0; i < MaxSignals; i++) Serials[i] = "";

                // Auto-detect and parse
                bool integrityOk = false;

                string content = Encoding.UTF8.GetString(fullBytes, 0, fullBytes.Length).Trim();

                // Check unambiguous format markers first. Broad substring checks for text
                // (e.g. Contains("A ")) false-positive on JSON/XML serial values like "Room A lobby".
                if (content.StartsWith("Type,Index,Value"))
                {
                    DetectedFormat = 2; // CSV
                    integrityOk = ParseCSV(fullBytes);
                }
                else if (content.StartsWith("<data>"))
                {
                    DetectedFormat = 3; // XML
                    integrityOk = ParseXML(fullBytes);
                }
                else if (content.StartsWith("{"))
                {
                    DetectedFormat = 4; // JSON
                    integrityOk = ParseJSON(fullBytes);
                }
                else if (content.Contains("[Digitals]"))
                {
                    DetectedFormat = 5; // INI
                    integrityOk = ParseINI(fullBytes);
                }
                else if (LooksLikeTextFormat(content))
                {
                    DetectedFormat = 1; // Text
                    integrityOk = ParseText(fullBytes);
                }
                else
                {
                    DetectedFormat = 6; // Binary
                    integrityOk = ParseBinary(fullBytes);
                }

                if (DetectedFormat == -1)
                {
                    Feedback = "Unable to detect format";
                    return -1;
                }

                if (!integrityOk)
                {
                    string formatStr = _formats[DetectedFormat - 1];
                    DetectedFormat = -2;
                    Feedback = string.Format("Integrity check failed for {0}", formatStr);
                    return -1;
                }

                Feedback = string.Format("Valid {0} detected and parsed{1}", _formats[DetectedFormat - 1], IsEncrypted() == 1 ? " (decrypted)" : "");
                return 0;
            }
            catch (Exception e)
            {
                Feedback = "Read Error: " + e.Message;
                return -1;
            }
        }

        private bool ParseText(byte[] fullBytes)
        {
            string fullContent = Encoding.UTF8.GetString(fullBytes, 0, fullBytes.Length);
            int crcIndex = fullContent.LastIndexOf("CRC32: ");
            if (crcIndex == -1)
            {
                return false;
            }
            string dataContent = fullContent.Substring(0, crcIndex);
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataContent);
            uint computedCrc = ComputeCRC32(dataBytes);
            string crcStr = fullContent.Substring(crcIndex + 7).Trim();
            uint storedCrc = 0;
            try
            {
                storedCrc = uint.Parse(crcStr);
            }
            catch
            {
                return false;
            }
            if (computedCrc != storedCrc)
            {
                return false;
            }

            // Parse data
            string[] lines = dataContent.Split('\n');
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine == "" || trimmedLine.Length == 0) continue;
                string[] parts = trimmedLine.Split(' ');
                if (parts.Length < 3) continue;
                int index = 0;
                try
                {
                    index = int.Parse(parts[1]);
                }
                catch
                {
                    continue;
                }
                if (index < 1 || index > MaxSignals) continue;
                index--;
                if (parts[0] == "D")
                {
                    ushort val = 0;
                    try
                    {
                        val = ushort.Parse(parts[2]);
                    }
                    catch
                    {
                        continue;
                    }
                    Digitals[index] = val;
                }
                else if (parts[0] == "A")
                {
                    ushort val = 0;
                    try
                    {
                        val = ushort.Parse(parts[2]);
                    }
                    catch
                    {
                        continue;
                    }
                    Analogs[index] = val;
                }
                else if (parts[0] == "S")
                {
                    string serial = trimmedLine.Substring(trimmedLine.IndexOf('"') + 1);
                    serial = serial.Substring(0, serial.LastIndexOf('"')).Replace("\"\"", "\"");
                    Serials[index] = serial;
                }
            }
            return true;
        }

        private bool ParseCSV(byte[] fullBytes)
        {
            string fullContent = Encoding.UTF8.GetString(fullBytes, 0, fullBytes.Length);
            int crcIndex = fullContent.LastIndexOf("CRC32: ");
            if (crcIndex == -1)
            {
                return false;
            }
            string dataContent = fullContent.Substring(0, crcIndex);
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataContent);
            uint computedCrc = ComputeCRC32(dataBytes);
            string crcStr = fullContent.Substring(crcIndex + 7).Trim();
            uint storedCrc = 0;
            try
            {
                storedCrc = uint.Parse(crcStr);
            }
            catch
            {
                return false;
            }
            if (computedCrc != storedCrc)
            {
                return false;
            }

            // Parse data (skip header)
            string[] lines = dataContent.Split('\n');
            for (int ln = 1; ln < lines.Length; ln++)
            {
                string trimmedLine = lines[ln].Trim();
                if (trimmedLine == "" || trimmedLine.Length == 0) continue;
                string[] parts = trimmedLine.Split(',');
                if (parts.Length < 3) continue;
                int index = 0;
                try
                {
                    index = int.Parse(parts[1]);
                }
                catch
                {
                    continue;
                }
                if (index < 1 || index > MaxSignals) continue;
                index--;
                if (parts[0] == "D")
                {
                    ushort val = 0;
                    try
                    {
                        val = ushort.Parse(parts[2]);
                    }
                    catch
                    {
                        continue;
                    }
                    Digitals[index] = val;
                }
                else if (parts[0] == "A")
                {
                    ushort val = 0;
                    try
                    {
                        val = ushort.Parse(parts[2]);
                    }
                    catch
                    {
                        continue;
                    }
                    Analogs[index] = val;
                }
                else if (parts[0] == "S")
                {
                    string serial = parts[2].Trim('"').Replace("\"\"", "\"");
                    Serials[index] = serial;
                }
            }
            return true;
        }

        private bool ParseXML(byte[] fullBytes)
        {
            string fullContent = Encoding.UTF8.GetString(fullBytes, 0, fullBytes.Length);
            int crcIndex = fullContent.LastIndexOf("<!-- CRC32: ");
            if (crcIndex == -1)
            {
                return false;
            }
            string dataContent = fullContent.Substring(0, crcIndex);
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataContent);
            uint computedCrc = ComputeCRC32(dataBytes);
            string crcStr = fullContent.Substring(crcIndex + 12, fullContent.LastIndexOf("-->") - (crcIndex + 12)).Trim();
            uint storedCrc = 0;
            try
            {
                storedCrc = uint.Parse(crcStr);
            }
            catch
            {
                return false;
            }
            if (computedCrc != storedCrc)
            {
                return false;
            }

            // Parse XML
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(dataContent);
            XmlNode dataNode = doc.SelectSingleNode("/data");

            XmlNode digitalsNode = dataNode.SelectSingleNode("digitals");
            for (int i = 0; i < MaxSignals; i++)
            {
                XmlNode dNode = digitalsNode.SelectSingleNode("d" + (i + 1));
                if (dNode != null)
                {
                    ushort val = 0;
                    try
                    {
                        val = ushort.Parse(dNode.InnerText);
                    }
                    catch
                    {
                        continue;
                    }
                    Digitals[i] = val;
                }
            }

            XmlNode analogsNode = dataNode.SelectSingleNode("analogs");
            for (int i = 0; i < MaxSignals; i++)
            {
                XmlNode aNode = analogsNode.SelectSingleNode("a" + (i + 1));
                if (aNode != null)
                {
                    ushort val = 0;
                    try
                    {
                        val = ushort.Parse(aNode.InnerText);
                    }
                    catch
                    {
                        continue;
                    }
                    Analogs[i] = val;
                }
            }

            XmlNode serialsNode = dataNode.SelectSingleNode("serials");
            for (int i = 0; i < MaxSignals; i++)
            {
                XmlNode sNode = serialsNode.SelectSingleNode("s" + (i + 1));
                if (sNode != null)
                {
                    Serials[i] = sNode.InnerText;
                }
            }
            return true;
        }

        /// <summary>
        /// Parse industry-standard JSON objects that expose digitals / analogs / serials arrays.
        /// Property names are case-insensitive. Arrays may be shorter than MaxSignals.
        /// Digital elements accept 0/1, integers, or JSON booleans.
        /// crc32 is optional: when present it is not required to match (re-serialize CRC was
        /// brittle and rejected valid external JSON). Missing arrays are left at defaults (0 / "").
        /// </summary>
        private bool ParseJSON(byte[] fullBytes)
        {
            string json = Encoding.UTF8.GetString(fullBytes, 0, fullBytes.Length).Trim();
            if (json.Length > 0 && json[0] == '\uFEFF')
            {
                json = json.Substring(1).Trim();
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception e)
            {
                Feedback = "JSON parse error: " + e.Message;
                return false;
            }

            if (root == null)
            {
                Feedback = "JSON parse returned null";
                return false;
            }

            /* Optional nested wrappers used by some exporters */
            JToken digToken = GetPropertyIgnoreCase(root, "digitals");
            JToken anaToken = GetPropertyIgnoreCase(root, "analogs");
            JToken serToken = GetPropertyIgnoreCase(root, "serials");
            if (digToken == null && anaToken == null && serToken == null)
            {
                JToken nested = GetPropertyIgnoreCase(root, "signalData");
                if (nested == null)
                    nested = GetPropertyIgnoreCase(root, "data");
                if (nested != null && nested.Type == JTokenType.Object)
                {
                    JObject inner = (JObject)nested;
                    digToken = GetPropertyIgnoreCase(inner, "digitals");
                    anaToken = GetPropertyIgnoreCase(inner, "analogs");
                    serToken = GetPropertyIgnoreCase(inner, "serials");
                }
            }

            if (digToken == null && anaToken == null && serToken == null)
            {
                Feedback = "JSON missing digitals/analogs/serials arrays";
                return false;
            }

            if (digToken != null && digToken.Type == JTokenType.Array)
            {
                ApplyUShortArray((JArray)digToken, Digitals);
            }
            if (anaToken != null && anaToken.Type == JTokenType.Array)
            {
                ApplyUShortArray((JArray)anaToken, Analogs);
            }
            if (serToken != null && serToken.Type == JTokenType.Array)
            {
                ApplyStringArray((JArray)serToken, Serials);
            }

            /* crc32 is optional metadata only — never reject industry-standard JSON for CRC */
            return true;
        }

        private static JToken GetPropertyIgnoreCase(JObject obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            foreach (JProperty prop in obj.Properties())
            {
                if (string.Compare(prop.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return prop.Value;
                }
            }
            return null;
        }

        private static void ApplyUShortArray(JArray arr, ushort[] dest)
        {
            if (arr == null || dest == null)
            {
                return;
            }
            int n = arr.Count < dest.Length ? arr.Count : dest.Length;
            for (int i = 0; i < n; i++)
            {
                dest[i] = JTokenToUShort(arr[i]);
            }
        }

        private static void ApplyStringArray(JArray arr, string[] dest)
        {
            if (arr == null || dest == null)
            {
                return;
            }
            int n = arr.Count < dest.Length ? arr.Count : dest.Length;
            for (int i = 0; i < n; i++)
            {
                JToken t = arr[i];
                if (t == null || t.Type == JTokenType.Null)
                {
                    dest[i] = "";
                }
                else
                {
                    dest[i] = t.ToString();
                }
            }
        }

        private static ushort JTokenToUShort(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null)
            {
                return 0;
            }
            try
            {
                if (t.Type == JTokenType.Boolean)
                {
                    return t.Value<bool>() ? (ushort)1 : (ushort)0;
                }
                if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float)
                {
                    long v = t.Value<long>();
                    if (v < 0) v = 0;
                    if (v > 65535) v = 65535;
                    return (ushort)v;
                }
                if (t.Type == JTokenType.String)
                {
                    string s = t.Value<string>();
                    if (s == null || s.Length == 0)
                    {
                        return 0;
                    }
                    if (string.Compare(s, "true", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return 1;
                    }
                    if (string.Compare(s, "false", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return 0;
                    }
                    return (ushort)int.Parse(s);
                }
                return (ushort)t.Value<long>();
            }
            catch
            {
                return 0;
            }
        }

        private bool ParseINI(byte[] fullBytes)
        {
            string fullContent = Encoding.UTF8.GetString(fullBytes, 0, fullBytes.Length);
            int crcIndex = fullContent.LastIndexOf("; CRC32: ");
            if (crcIndex == -1)
            {
                return false;
            }
            string dataContent = fullContent.Substring(0, crcIndex);
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataContent);
            uint computedCrc = ComputeCRC32(dataBytes);
            string crcStr = fullContent.Substring(crcIndex + 9).Trim();
            uint storedCrc = 0;
            try
            {
                storedCrc = uint.Parse(crcStr);
            }
            catch
            {
                return false;
            }
            if (computedCrc != storedCrc)
            {
                return false;
            }

            // Parse INI
            string[] lines = dataContent.Split('\n');
            string section = "";
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed == "" || trimmed.Length == 0) continue;
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    section = trimmed.Substring(1, trimmed.Length - 2);
                    continue;
                }
                if (!trimmed.Contains("=")) continue;
                string[] parts = trimmed.Split('=');
                int index = 0;
                try
                {
                    index = int.Parse(parts[0]);
                }
                catch
                {
                    continue;
                }
                if (index < 1 || index > MaxSignals) continue;
                index--;
                string val = parts[1].Replace("\\=", "=");
                if (section == "Digitals")
                {
                    ushort dVal = 0;
                    try
                    {
                        dVal = ushort.Parse(val);
                    }
                    catch
                    {
                        continue;
                    }
                    Digitals[index] = dVal;
                }
                else if (section == "Analogs")
                {
                    ushort aVal = 0;
                    try
                    {
                        aVal = ushort.Parse(val);
                    }
                    catch
                    {
                        continue;
                    }
                    Analogs[index] = aVal;
                }
                else if (section == "Serials")
                {
                    Serials[index] = val;
                }
            }
            return true;
        }

        private bool ParseBinary(byte[] fullBytes)
        {
            if (fullBytes.Length < 4)
            {
                return false;
            }
            int crcOffset = fullBytes.Length - 4;
            byte[] dataBytes = new byte[crcOffset];
            Array.Copy(fullBytes, 0, dataBytes, 0, crcOffset);
            uint computedCrc = ComputeCRC32(dataBytes);
            byte[] crcBytes = new byte[4];
            Array.Copy(fullBytes, crcOffset, crcBytes, 0, 4);
            uint storedCrc = BitConverter.ToUInt32(crcBytes, 0);
            if (computedCrc != storedCrc)
            {
                return false;
            }

            // Parse binary
            using (MemoryStream ms = new MemoryStream(dataBytes))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    // Digitals
                    int dCount = br.ReadInt32();
                    for (int c = 0; c < dCount; c++)
                    {
                        byte idx = br.ReadByte();
                        if (idx < MaxSignals)
                            Digitals[idx] = br.ReadUInt16();
                    }

                    // Analogs
                    int aCount = br.ReadInt32();
                    for (int c = 0; c < aCount; c++)
                    {
                        byte idx = br.ReadByte();
                        if (idx < MaxSignals)
                            Analogs[idx] = br.ReadUInt16();
                    }

                    // Serials
                    int sCount = br.ReadInt32();
                    for (int c = 0; c < sCount; c++)
                    {
                        byte idx = br.ReadByte();
                        int len = br.ReadInt32();
                        byte[] sBytes = br.ReadBytes(len);
                        if (idx < MaxSignals)
                            Serials[idx] = Encoding.UTF8.GetString(sBytes, 0, sBytes.Length);
                    }
                }
            }
            return true;
        }
    }
}