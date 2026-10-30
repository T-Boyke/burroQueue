using burroQueue.Domain.Model;

namespace burroQueue.Domain.Services;

public static class OfficeVibePolicy
{
    public static int CalculateRequiredSkipVotes(TrackVibeProfile vibeProfile, int defaultRequiredVotes = 3)
    {
        int baseline = Math.Max(1, defaultRequiredVotes);
        return vibeProfile.IsDisruptiveToDeepWork ? 1 : baseline;
    }
}
