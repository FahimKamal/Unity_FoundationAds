namespace Pancake
{
    public enum EResetType
    {
        /// <summary>
        /// Each scene loaded by LoadSceneMode.Single
        /// </summary>
        SceneLoaded = 0,

        /// <summary>
        /// Each scene loaded by LoadSceneMode.Additive.<br/>
        /// Use this option for compatibility with the use of LoadSceneMode.Additive instead of LoadSingle Scene introduced in foundation,
        /// to keep the variable's value reset behavior similar to SceneLoaded. <br/>
        /// If you are not using a flow load scene like in foundation, or you are not sure how to reset the value when the load scene is adaptive, do not use this option.
        /// </summary>
        AdditiveSceneLoaded = 2,
        ApplicationStarts = 1,
    }

    public enum EStartupMode
    {
        Manual,
        Awake,
        Start,
        OnEnabled
    }

    public enum ETargetFrameRate
    {
        ByDevice = -1,
        Frame60 = 60,
        Frame120 = 120,
        Frame240 = 240
    }

    public enum ECreationMode
    {
        Auto,
        Manual
    }

    [System.Flags]
    public enum EGameLoopType
    {
        None = 0,
        Update = 1 << 0,
        FixedUpdate = 1 << 1,
        LateUpdate = 1 << 2,
    }

    public enum EAlignment
    {
        Left,
        Right,
        Top,
        Bottom,
        Center
    }

    public enum EFourDirection
    {
        Left,
        Right,
        Top,
        Down,
    }
}