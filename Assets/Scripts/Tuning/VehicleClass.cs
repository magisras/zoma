namespace TwentyTons.Tuning
{
    /// <summary>
    /// Every kind of thing that moves on the road, ordered from lightest to heaviest.
    ///
    /// The order matters: "size is right of way" (RESEARCH.md, Driving culture). A heavier class
    /// pushes, a lighter class yields, unless nerve calls the bluff. Keep this enum sorted by mass
    /// so a simple comparison (a > b) answers "who should give way?".
    /// </summary>
    public enum VehicleClass
    {
        Pedestrian = 0,
        Rickshaw = 1,
        Cng = 2,        // the green three-wheeler auto-rickshaw, called a "CNG" in Dhaka
        Car = 3,
        Truck = 4,
        Bus = 5
    }
}
