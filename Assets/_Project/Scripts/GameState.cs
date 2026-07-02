#nullable enable
namespace Mehawar.Greybox
{
    /// <summary>The two campaigns (CLAUDE.md locked decision). The choice sets the avatar.</summary>
    public enum Campaign
    {
        ViaOscura,   // Lucius, host of Tiamat — aggression (Bloodlust/Fury)
        ViaRomana    // Cesare, empowered by Marduk — discipline (moveset from STEP 3)
    }

    /// <summary>
    /// Minimal persistent game state (campaign, current level). Plain static service on purpose:
    /// no disk persistence yet, survives level rebuilds within a session, resets on domain reload.
    /// When saving arrives, this becomes the serialization surface.
    /// </summary>
    public static class GameState
    {
        public static Campaign Campaign = Campaign.ViaOscura;
        public static int CurrentLevel = 1;

        /// <summary>Interludes already shown this session (no disk persistence by design):
        /// death, replay and re-completion never replay a seen beat.</summary>
        public static readonly System.Collections.Generic.HashSet<string> SeenInterludes =
            new System.Collections.Generic.HashSet<string>();

        public static string InterludeKey(Campaign campaign, int completedLevel)
            => campaign + ":" + completedLevel;

        public static string AvatarName => Campaign == Campaign.ViaOscura ? "Lucius" : "Cesare";
        public static string CampaignName => Campaign == Campaign.ViaOscura ? "Via Oscura" : "Via Romana";
    }
}
