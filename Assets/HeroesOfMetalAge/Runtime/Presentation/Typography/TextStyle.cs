namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// What a text is for; <see cref="TypographySettings"/> gives each style its font and look. Values are stored in prefabs,
    /// so existing values never change; new styles go at the end.
    /// </summary>
    public enum TextStyle
    {
        /// <summary>Large dramatic titles: the intro's title, VICTORY, SPOILS OF WAR, DEFEAT.</summary>
        Display,
        /// <summary>Screen and dialog titles and names: hero, town, encounter, speaker.</summary>
        Title,
        /// <summary>Panel headings inside a screen: YOUR WARHOST, MISSION, EQUIPMENT.</summary>
        Heading,
        /// <summary>Button captions.</summary>
        Button,
        /// <summary>Small captions and stat names: MOVEMENT, ATTACK, slot names, the footer help.</summary>
        Label,
        /// <summary>Readable running text: descriptions, objectives, dialogue lines, the battle chronicle, notices.</summary>
        Body,
        /// <summary>Counts and amounts: stack sizes, gold, day, stats.</summary>
        Number,
        /// <summary>Text written on parchment: the mission briefing and the rolling credits.</summary>
        Scroll,
        /// <summary>The intro's typed verses.</summary>
        Verse,
        /// <summary>Numbers and words over the world: damage numbers, map labels.</summary>
        Floating
    }
}
