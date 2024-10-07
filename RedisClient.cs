using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace RedisLib;

public static class RedisClient
{
    private static ConnectionMultiplexer? redisconnection;

    private static JsonSerializerSettings jsonsettings = new JsonSerializerSettings() {
        TypeNameHandling = TypeNameHandling.Objects,
        TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple
    };

    public static void SetupConnection(string connectionstring)
    {
        try
        {
            if (redisconnection != null)
            {
                redisconnection.Close();
                redisconnection.Dispose();
            }

            var configoption = ConfigurationOptions.Parse(connectionstring);

            configoption.AbortOnConnectFail = false;
            configoption.ConnectTimeout = 3000;
            configoption.SyncTimeout = 10000;
            configoption.AsyncTimeout = 10000;
            configoption.CertificateValidation += PerformPaperChecking;

            redisconnection = ConnectionMultiplexer.Connect(configoption);
        }
        catch (Exception ex)
        {
            reporterror(ex, "SetupConnection");
        }
    }

    static bool PerformPaperChecking(
        object sender,
        X509Certificate? certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors
    )
    {
        return true;
    }

    public static void reporterror(Exception ex, string method = "", params string[] extinfo)
    {
        Console.WriteLine(
            $"!!|RedisLib| {DateTime.UtcNow.ToString("0")} : {method} {string.Join("||", extinfo)} : ({ex.GetType().ToString()}) {ex.Message}"
        );
        Console.WriteLine(ex.StackTrace);
    }

    public async static Task StringSet(
        string key,
        string value,
        TimeSpan? expiry = null,
        int redisdatabase = -1
    )
    {
        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            await database.StringSetAsync(key, value, expiry);
        }
        catch (Exception ex)
        {
            reporterror(ex, "StringSet", key);
        }
    }

    public async static Task<bool> KeyExpiry(string key, TimeSpan expiry, int redisdatabase = -1)
    {
        if (redisconnection == null)
            return false;

        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            return await database.KeyExpireAsync(key, expiry, ExpireWhen.Always, CommandFlags.FireAndForget);
        }
        catch (Exception ex)
        {
            reporterror(ex, "KeyExpire", key);
            return false;
        }
    }

    public async static Task<bool> KeyExists(string key, int redisdatabase = -1)
    {
        if (redisconnection == null)
            return false;

        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            return await database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            reporterror(ex, "StringGet", key);
        }

        return false;
    }

    public async static Task<string>? StringGet(string key, int redisdatabase = -1)
    {
        if (redisconnection == null)
            return null;

        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            return await database.StringGetAsync(key);
        }
        catch (Exception ex)
        {
            reporterror(ex, "StringGet", key);
        }

        return null;
    }

    public async static void ObjectSet(
        string key,
        object value,
        TimeSpan? expiry = null,
        int redisdatabase = -1
    )
    {
        //Don't deal with nulls
        if (value == null)
            return;
        //serialize the object to json and then save it
        var jsonstring = JsonConvert.SerializeObject(value, jsonsettings);
        await StringSet(key, jsonstring, expiry, redisdatabase);
    }

    /// <summary>
    /// Try and deserialize the object of the given type from Redis
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns></returns>
    public async static Task<T>? ObjectGet<T>(string key, int redisdatabase = -1)
    {
        var jsonstring = await StringGet(key, redisdatabase);
        if (jsonstring == null)
            return default(T);

        

        return JsonConvert.DeserializeObject<T>(jsonstring, jsonsettings);
    }

    /// <summary>
    /// Call INCR on the given key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="redisdatabase"></param>
    /// <returns>Value of the key</returns>
    public async static Task<long> KeyIncr(string key, int redisdatabase = -1)
    {
        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            return await database.StringIncrementAsync(key);
        }
        catch (Exception ex)
        {
            reporterror(ex, "KeyIncr", key);
        }
        return 0;
    }

    /// <summary>
    /// Call DECR on the given key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="redisdatabase"></param>
    /// <returns>Value of the key</returns>
    public async static Task<long> KeyDecr(string key, int redisdatabase = -1)
    {
        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            return await database.StringDecrementAsync(key);
        }
        catch (Exception ex)
        {
            reporterror(ex, "KeyDecr", key);
        }
        return 0;
    }

    /// <summary>
    /// Execute PFADD command
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="redisdatabase"></param>
    /// <returns></returns>
    public static bool HyperLogAdd(string key, string value, int redisdatabase = -1)
    {
        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            //Use fire and forget cause we just don't care
            return database.HyperLogLogAdd(key, value, CommandFlags.FireAndForget);
        }
        catch (Exception ex)
        {
            reporterror(ex, "HyperLogAdd", key);
        }
        return false;
    }

    /// <summary>
    /// Executes the PFCOUNT command
    /// </summary>
    /// <param name="key"></param>
    /// <param name="redisdatabase"></param>
    /// <returns></returns>
    public static long HyperLogCount(string key, int redisdatabase = -1)
    {
        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            //Use fire and forget cause we just don't care
            return database.HyperLogLogLength(key);
        }
        catch (Exception ex)
        {
            reporterror(ex, "HyperLogCount", key);
        }
        return 0;
    }

    /// <summary>
    /// Deletes the specified redis key
    /// </summary>
    /// <param name="key"></param>
    public static void KeyDelete(string key, int redisdatabase = -1)
    {
        try
        {
            var database = redisconnection.GetDatabase(redisdatabase);
            database.KeyDelete(key);
        }
        catch (Exception ex)
        {
            reporterror(ex, "KeyDelete", key);
        }
    }

    /// <summary>
    /// Get the object from cache and if it doesn't exists try and store it
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="getaction"></param>
    /// <returns></returns>
    /// <remarks>
    /// In the getaction, do what is needed to get the object then set it to the
    /// cacheparam.value to store it into redis, null values will not be stored.
    /// Also set cacheparam.expiry in the getaction to set it's expiry.
    /// </remarks>
    public async static Task<T> GetOrCacheObject<T>(
        string key,
        Func<CacheParams<T>, Task> getaction,
        int redisdatabase = -1
    )
    {
        //If redis connection isn't working then just
        //invoke the function to retrieve the data and return it
        if (redisconnection == null || redisconnection.IsConnected == false)
        {
            //Invoke the action and return the value
            var oparam = new CacheParams<T>();
            oparam.cachekey = key;

            //invoke getaction
            await getaction(oparam);
            return oparam.value;
        }

        //Try and get the value first
        var objresults = await ObjectGet<T>(key, redisdatabase);
        if (objresults == null)
        {
            var oparam = new CacheParams<T>();
            oparam.cachekey = key;

            //invoke getaction
            await getaction(oparam);

            if (oparam.value != null)
            {
                //Store the value if it was found
                ObjectSet(key, oparam.value, oparam.expiry, redisdatabase);
                objresults = oparam.value;
            }
        }

        return objresults;
    }
}

/// <summary>
/// Cache parameters in the getorcreate functions
/// </summary>
public class CacheParams<T>
{
    /// <summary>
    /// To override the cache key if needed
    /// </summary>
    public string cachekey { get; set; }

    /// <summary>
    /// Value to be JSON serialized and inserted into the cache
    /// </summary>
    public T value { get; set; }

    /// <summary>
    /// Expiry timespan which will be set if not zero
    /// </summary>
    public TimeSpan? expiry { get; set; }
}
