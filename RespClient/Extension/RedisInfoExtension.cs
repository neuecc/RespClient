using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Redis.Protocol;

namespace Redis.Extension
{
    static class RedisInfoExtension
    {
        /// <summary>
        /// Convert RedisString to RedisInfo
        /// </summary>
        /// <param name="source">input Redis info returned item</param>
        /// <returns></returns>
        public static IEnumerable<RedisInfo> AsRedisInfo(this object source)
        {
            // sample source input
            /*
            var source = @"# MEMORY
            key1:value1
            key2:value2

            # SERVER
            key3:sub1=value3,sub2=value4
            key4:sub3=value5,sub4=value6
            # HOGE
            key5:sub5=value7"
            */

            // Get IEnumerable<RedisInfo>
            var info = source.CastRedisString()
                .SelectMany(x => x.Split(new[] { "#" }, StringSplitOptions.RemoveEmptyEntries))
                .SelectMany(group =>
                {
                    var keys = group.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    // keep first line as infoType
                    var infoType = keys.First().Replace("#", "").Trim();

                    // skip first line and start for each key
                    return keys.Skip(1).SelectMany(y =>
                    {
                        var kvs = y.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        // first item is key
                        var key = kvs.First();

                        // skip first item and start subkey to create <RedisInfo>
                        var values = kvs.Skip(1).SelectMany(value => value
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(sub =>
                            {
                                // Split subkey with =, element 1 means no subkey. The other means contains subkey. => all together into <RedisInfo>
                                var subKvOrValue = sub.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                                return subKvOrValue.Length == 1 ?
                                    new RedisInfo(infoType, key, string.Empty, subKvOrValue.First())
                                    :
                                    new RedisInfo(infoType, key, subKvOrValue.First(), subKvOrValue.Last());
                            }));

                        return values;
                    });
                });
            return info;
        }

        /// <summary>
        /// Filter source output with Key and SubKey
        /// </summary>
        /// <param name="source"></param>
        /// <param name="Key"></param>
        /// <param name="SubKey"></param>
        /// <returns></returns>
        public static IEnumerable<RedisInfo> FilterKeySubKey(this System.Collections.Generic.IEnumerable<RedisInfo> source, RedisInfoKeyType[] Key, RedisInfoSubkeyType[] SubKey)
        {
            return source.Select(item =>
            {
                RedisInfo values = null;
                if (Key == null & SubKey == null)
                {
                    // When both Key and Subkey not specified
                    values = item;
                }
                else if (Key != null & SubKey == null)
                {
                    // when Key specify and output contains it
                    if (Key.Where(x => x.ToString() == item.Key).Any())
                        values = item;
                }
                else if (Key == null & SubKey != null)
                {
                    // when Subkey specify and output contains it
                    if (SubKey.Where(x => x.ToString() == item.SubKey).Any())
                        values = item;
                }
                else if (Key.Where(x => x.ToString() == item.Key).Any() & SubKey.Where(x => x.ToString() == item.SubKey).Any())
                {
                    // when Key and Subkey specify and output contains both
                    values = item;
                }
                return values;
            });
        }

    }
}
