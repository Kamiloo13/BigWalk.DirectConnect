namespace BigWalk.DirectConnect.Tuning;

/// <summary>When <see cref="SendRateTuner"/> raises Mirror's send rate.</summary>
public enum TuningMode
{
    /// <summary>
    /// Client: only while connected over KCP (join via IP).
    /// Host: only while every remote player has an IP address (nobody joined via code / Epic).
    /// </summary>
    DirectIpOnly,

    /// <summary>In every session, including Epic (join code) sessions.</summary>
    Always,

    /// <summary>Never change the send rate.</summary>
    Off,
}
