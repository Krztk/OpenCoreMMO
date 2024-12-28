﻿namespace NeoServer.Scripts.LuaJIT;

public class ArgManager
{
    #region Properties

    public string ExePath { get; set; }

    #endregion

    #region Instance

    private static ArgManager _instance;

    public static ArgManager GetInstance()
    {
        return _instance == null ? _instance = new ArgManager() : _instance;
    }

    #endregion
}