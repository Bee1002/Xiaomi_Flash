using System;
using System.Collections.Generic;

namespace Xiaomi_Flash
{
    class FastbootData
    {
        public Dictionary<string, long> partition_size;
        public Dictionary<string, bool?> partition_is_logical;
        public string? product;
        public bool secure;
        public string? current_slot;
        public bool fastbootd;
        public long max_download_size;
        public string? snapshot_update_status;
        public Dictionary<string, string> bootloader_vars;

        public FastbootData(string real_raw_data)
        {
            partition_size = new Dictionary<string, long>();
            partition_is_logical = new Dictionary<string, bool?>();
            product = null;
            secure = false;
            current_slot = null;
            fastbootd = false;
            max_download_size = -1;
            snapshot_update_status = null;

            foreach (string line in real_raw_data.Split(new char[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                List<string> tokens = new List<string>(line.Split(new char[] { ' ', ':', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries));

                if (tokens.Count < 2 || !tokens[0].Contains("bootloader"))
                    continue;

                if (tokens[1] == "partition-size" && tokens.Count >= 4)
                {
                    string raw_size = tokens[3].Replace("0x", "");
                    try
                    {
                        partition_size.Add(tokens[2], Convert.ToInt64(raw_size, 16));
                    }
                    catch (Exception)
                    {
                        partition_size[tokens[2]] = -1;
                    }
                    continue;
                }

                if (tokens[1] == "is-logical" && tokens.Count >= 4)
                {
                    try
                    {
                        partition_is_logical.Add(tokens[2], tokens[3] == "yes");
                    }
                    catch (Exception)
                    {
                        partition_is_logical[tokens[2]] = null;
                    }
                    continue;
                }

                if (tokens[1] == "product" && tokens.Count >= 3)
                {
                    product = tokens[2];
                    continue;
                }

                if (tokens[1] == "secure" && tokens.Count >= 3)
                {
                    secure = tokens[2] == "yes";
                    continue;
                }

                if (tokens[1] == "current-slot" && tokens.Count >= 3)
                {
                    current_slot = tokens[2];
                    continue;
                }

                if (tokens[1] == "is-userspace" && tokens.Count >= 3)
                {
                    fastbootd = tokens[2] == "yes";
                    continue;
                }

                if (tokens[1] == "max-download-size" && tokens.Count >= 3)
                {
                    try
                    {
                        max_download_size = Convert.ToInt64(tokens[2], 16);
                    }
                    catch (Exception) { }
                    continue;
                }

                if (tokens[1] == "snapshot-update-status" && tokens.Count >= 3)
                {
                    snapshot_update_status = tokens[2];
                }
            }

            bootloader_vars = FastbootAllVars.Parse(real_raw_data);
        }
    }
}
