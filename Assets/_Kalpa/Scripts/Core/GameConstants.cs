// ============================================================================
// GameConstants.cs
// ----------------------------------------------------------------------------
// Central location for all game-wide constants.
// Modify values here to tune the game without hunting through code.
// ============================================================================

namespace Kalpa.Core
{
    /// <summary>
    /// Immutable, game-wide configuration constants.
    /// All tunable magic numbers should live here — never inline in logic.
    /// </summary>
    public static class GameConstants
    {
        // --------------------------------------------------------------------
        // World / Chunk configuration
        // --------------------------------------------------------------------

        /// <summary>Size of a single chunk on X and Z axes (blocks).</summary>
        public const int ChunkSize = 16;

        /// <summary>Height of a single chunk on Y axis (blocks).</summary>
        public const int ChunkHeight = 128;

        /// <summary>How many chunks to load around the player (radius).</summary>
        public const int RenderDistance = 4;

        /// <summary>Sea level Y coordinate for terrain generation.</summary>
        public const int SeaLevel = 32;

        // --------------------------------------------------------------------
        // Block system
        // --------------------------------------------------------------------

        /// <summary>Reserved block ID for empty space (air).</summary>
        public const byte AirBlockId = 0;

        /// <summary>Maximum number of block types supported.</summary>
        public const int MaxBlockTypes = 256;

        // --------------------------------------------------------------------
        // Player physics
        // --------------------------------------------------------------------

        public const float PlayerHeight = 1.8f;
        public const float PlayerWidth = 0.6f;
        public const float PlayerWalkSpeed = 5.5f;
        public const float PlayerSprintSpeed = 8.5f;
        public const float PlayerJumpForce = 8.0f;
        public const float Gravity = 24.0f;
        public const float TerminalVelocity = 50.0f;

        /// <summary>Max distance (blocks) player can reach to break/place.</summary>
        public const float PlayerReach = 5.0f;

        // --------------------------------------------------------------------
        // Save system
        // --------------------------------------------------------------------

        public const string SaveFolder = "Saves";
        public const string SaveExtension = ".kalpa";
        public const int SaveFormatVersion = 1;

        // --------------------------------------------------------------------
        // Version
        // --------------------------------------------------------------------

        public const string GameVersion = "0.1.0";
        public const string GameName = "Kalpa";

        // --------------------------------------------------------------------
        // Attribution / Copyright
        // --------------------------------------------------------------------

        public const string Author = "cenarjun";
        public const string Copyright = "© 2026 cenarjun. All rights reserved.";
        public const string Studio = "cenarjun-studios";   // change to a studio name later if you want
    }
}
