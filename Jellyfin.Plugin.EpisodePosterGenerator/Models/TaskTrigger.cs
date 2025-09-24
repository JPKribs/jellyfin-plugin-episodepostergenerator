namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public enum TaskTrigger
    {
        /// <summary>
        /// Triggered by scheduled task - results should be uploaded to Jellyfin and tracked in database.
        /// </summary>
        Task,

        /// <summary>
        /// Triggered by image provider - results should be returned as memory stream for immediate use.
        /// </summary>
        Provider
    }
}