namespace burroQueue.Domain.Model;

public readonly record struct TrackVibeProfile(
    float RmsEnergy,
    float ZeroCrossingRate,
    float CrestFactor,
    bool IsDisruptiveToDeepWork,
    string ClassificationLabel);

