namespace TwentyTons.Core
{
    /// <summary>
    /// A police box beside the road (docs/STREET_CONTROL.md §3): a sergeant may be on duty, and if he
    /// is, he may step out as you pass. Separate from the officer at the junction, who controls
    /// flow and does not file cases.
    /// </summary>
    public sealed class Checkpoint
    {
        public string Name;
        public float S;
        public bool SergeantOnDuty;
    }
}
