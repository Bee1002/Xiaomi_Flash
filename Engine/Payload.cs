using ChromeosUpdateEngine;
using Google.Protobuf;
using Ionic.BZip2;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xiaomi_Flash.Compression;

namespace Xiaomi_Flash
{
    public class Payload : IDisposable
    {
        string payload_tmp = null!;
        BinaryReader? binaryReader;
        public class PayloadInitException : Exception
        {
            public PayloadInitException(string message) : base(message) { }
        };
        public class PayloadExtractionException : Exception
        {
            public PayloadExtractionException(string message) : base(message) { }
        };

        const string magic = "CrAU";
        public UInt64 file_format_version;
        public UInt64 manifest_size;
        public UInt32 metadata_signature_size;
        public DeltaArchiveManifest manifest = null!;
        public Signatures metadata_signature_message = null!;
        //{data blocks}
        public UInt64 payload_signatures_message_size;
        public Signatures payload_signatures_message = null!;

        long data_start;
        public ulong data_size;

        public PayloadExtractionException? extract(string which, string path,
            bool ignore_unknown_op, bool ignore_checks)
        {
            BinaryReader reader = binaryReader ?? throw new InvalidOperationException("Payload reader is not initialized.");
            SHA256 Sha256 = SHA256.Create();

            foreach (PartitionUpdate partitionUpdate in manifest.Partitions)
            {
                if (partitionUpdate.PartitionName != which)
                    continue;

                using (FileStream fileStream = new FileStream(path + "\\" + which + ".img", FileMode.Create))
                {
                    foreach (InstallOperation installOperation in partitionUpdate.Operations)
                    {
                        reader.BaseStream.Seek(data_start + (long)installOperation.DataOffset, SeekOrigin.Begin);
                        byte[] raw_data = reader.ReadBytes((int)installOperation.DataLength);
                        if (!ignore_checks && installOperation.HasDataSha256Hash &&
                            installOperation.DataSha256Hash.ToBase64() != Convert.ToBase64String(Sha256.ComputeHash(raw_data)))
                            return new PayloadExtractionException("Block hash check failed");

                        if (installOperation.DstExtents == null)
                            return new PayloadExtractionException("No dst");

                        if (installOperation.DstExtents.Count > 1)
                            return new PayloadExtractionException("Multiple dst in one operation");

                        long dst_start = (long)installOperation.DstExtents[0].StartBlock * manifest.BlockSize;
                        long dst_length = (long)installOperation.DstExtents[0].NumBlocks * manifest.BlockSize;

                        fileStream.Seek(dst_start, SeekOrigin.Begin);

                        using (MemoryStream raw_data_stream = new MemoryStream(raw_data))
                        {
                            switch (installOperation.Type)
                            {
                                case InstallOperation.Types.Type.Replace:
                                    if (ignore_checks || (long)installOperation.DataLength == dst_length)
                                        raw_data_stream.CopyTo(fileStream);
                                    else
                                        return new PayloadExtractionException("REPLACE: Block size mismatch");
                                    break;
                                case InstallOperation.Types.Type.ReplaceBz:
                                    using (MemoryStream buf = new MemoryStream())
                                    {
                                        using (BZip2InputStream bZip = new BZip2InputStream(raw_data_stream))
                                        {
                                            bZip.CopyTo(buf);
                                        }

                                        if (ignore_checks || buf.Length == dst_length)
                                        {
                                            buf.Seek(0, SeekOrigin.Begin);
                                            buf.CopyTo(fileStream);
                                        }
                                        else
                                            return new PayloadExtractionException("BZ: Block size mismatch");
                                    }
                                    break;
                                case InstallOperation.Types.Type.ReplaceXz:
                                    using (MemoryStream buf = new MemoryStream())
                                    {
                                        using (LzmaInputStream xZ = new LzmaInputStream(raw_data_stream))
                                        {
                                            xZ.CopyTo(buf);
                                        }

                                        if (ignore_checks || buf.Length == dst_length)
                                        {
                                            buf.Seek(0, SeekOrigin.Begin);
                                            buf.CopyTo(fileStream);
                                        }
                                        else
                                            return new PayloadExtractionException("XZ: Block size mismatch");
                                    }
                                    break;
                                case InstallOperation.Types.Type.Zero:
                                    long i = dst_length;
                                    while (i-- != 0)
                                        fileStream.WriteByte(0);
                                    break;
                                default:
                                    if (!ignore_unknown_op)
                                        return new PayloadExtractionException("Unknown action type " + installOperation.Type.ToString());
                                    break;
                            }
                        }
                    }

                    fileStream.Seek(0, SeekOrigin.Begin);
                    if (!ignore_checks && partitionUpdate.NewPartitionInfo != null &&
                        (fileStream.Length != (long)partitionUpdate.NewPartitionInfo.Size
                        || (partitionUpdate.NewPartitionInfo.HasHash &&
                        Convert.ToBase64String(Sha256.ComputeHash(fileStream)) != partitionUpdate.NewPartitionInfo.Hash.ToBase64())))
                        return new PayloadExtractionException("Final image check failed");
                }

                return null;
            }

            return new PayloadExtractionException("Unable to find target");
        }
        public PayloadInitException? init()
        {
            BinaryReader reader = binaryReader ?? throw new InvalidOperationException("Payload reader is not initialized.");
            try
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != magic)
                    return new PayloadInitException("Magic mismatch");

                file_format_version = BitConverter.ToUInt64(reader.ReadBytes(8).Reverse().ToArray(), 0);

                if (file_format_version < 2)
                    return new PayloadInitException("format version 1 is not supported");

                manifest_size = BitConverter.ToUInt64(reader.ReadBytes(8).Reverse().ToArray(), 0);
                metadata_signature_size = BitConverter.ToUInt32(reader.ReadBytes(4).Reverse().ToArray(), 0);

                if (manifest_size > Int32.MaxValue)
                    return new PayloadInitException("manifest_size overflowed");

                manifest = new MessageParser<DeltaArchiveManifest>(delegate { return new DeltaArchiveManifest(); })
                    .ParseFrom(reader.ReadBytes((int)manifest_size));

                if (metadata_signature_size > Int32.MaxValue)
                    return new PayloadInitException("metadata_signature_size overflowed");

                metadata_signature_message = new MessageParser<Signatures>(delegate { return new Signatures(); })
                    .ParseFrom(reader.ReadBytes((int)metadata_signature_size));

                data_start = reader.BaseStream.Position;
                data_size = manifest.SignaturesOffset;

                reader.BaseStream.Seek((long)manifest.SignaturesOffset, SeekOrigin.Current);
                payload_signatures_message_size = manifest.SignaturesSize;
                if (payload_signatures_message_size > Int32.MaxValue)
                    return new PayloadInitException("payload_signatures_message_size overflowed");
                payload_signatures_message = new MessageParser<Signatures>(delegate { return new Signatures(); })
                    .ParseFrom(reader.ReadBytes((int)payload_signatures_message_size));

            }
            catch (Exception e)
            {
                return new PayloadInitException(e.Message);
            }

            return null;
        }

        public void Dispose()
        {
            if (binaryReader != null)
            {
                binaryReader.Close();
                binaryReader = null;
            }
            try
            {
                new DirectoryInfo(payload_tmp).Delete(true);
            }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
        }

        public Payload(string path, String tmpdir)
        {
            payload_tmp = tmpdir;

            if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (!entry.Name.Equals("payload.bin", StringComparison.OrdinalIgnoreCase))
                            continue;

                        Directory.CreateDirectory(payload_tmp);
                        string extractedPath = Path.Combine(payload_tmp, "payload.bin");
                        entry.ExtractToFile(extractedPath, overwrite: true);
                        binaryReader = new BinaryReader(new FileStream(extractedPath, FileMode.Open));
                        return;
                    }
                }
                throw new Exception("Unable to find entry for payload.bin");
            }
            binaryReader = new BinaryReader(new FileStream(path, FileMode.Open));
        }

        ~Payload()
        {
            Dispose();
        }
    }
}
