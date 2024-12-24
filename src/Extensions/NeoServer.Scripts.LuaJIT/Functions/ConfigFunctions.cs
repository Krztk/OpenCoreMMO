﻿using LuaNET;
using Serilog;

namespace NeoServer.Scripts.LuaJIT.Functions;
public class ConfigFunctions : LuaScriptInterface, IConfigFunctions
{
    public ConfigFunctions(
        ILuaEnvironment luaEnvironment, 
        ILogger logger) : base(nameof(ConfigFunctions))
    {
    }

    public void Init(LuaState L)
    {
        RegisterTable(L, "configManager");
        RegisterMethod(L, "configManager", "getString", LuaConfigManagerGetString);
        RegisterMethod(L, "configManager", "getNumber", LuaConfigManagerGetNumber);
        RegisterMethod(L, "configManager", "getBoolean", LuaConfigManagerGetBoolean);
        RegisterMethod(L, "configManager", "getFloat", LuaConfigManagerGetFloat);

        RegisterTable(L, "configKeys");

        //RegisterVariable(L, "configKeys", "ALLOW_CHANGEOUTFIT", BooleanConfig.ALLOW_CHANGEOUTFIT);

        foreach (var item in Enum.GetValues<BooleanConfigType>())
            RegisterVariable(L, "configKeys", item.ToString(), item);

        foreach (var item in Enum.GetValues<StringConfigType>())
            RegisterVariable(L, "configKeys", item.ToString(), item);

        foreach (var item in Enum.GetValues<IntegerConfigType>())
            RegisterVariable(L, "configKeys", item.ToString(), item);

        foreach (var item in Enum.GetValues<FloatingConfigType>())
            RegisterVariable(L, "configKeys", item.ToString(), item);

        RegisterVariable(L, "configKeys", "BASE_DIRECTORY", AppContext.BaseDirectory);
    }

    private static int LuaConfigManagerGetString(LuaState L)
    {
        // configManager:getString()
        PushString(L, ConfigManager.GetInstance().GetString(GetNumber<StringConfigType>(L, -1)));
        return 1;
    }

    private static int LuaConfigManagerGetNumber(LuaState L)
    {
        // configManager:getNumber()
        Lua.PushNumber(L, ConfigManager.GetInstance().GetNumber(GetNumber<IntegerConfigType>(L, -1)));
        return 1;
    }

    private static int LuaConfigManagerGetBoolean(LuaState L)
    {
        // configManager:getBoolean()
        PushBoolean(L, ConfigManager.GetInstance().GetBoolean(GetNumber<BooleanConfigType>(L, -1)));
        return 1;
    }

    private static int LuaConfigManagerGetFloat(LuaState L)
    {
        // configManager:getFloat()
        Lua.PushNumber(L, ConfigManager.GetInstance().GetFloat(GetNumber<FloatingConfigType>(L, -1)));
        return 1;
    }
}
