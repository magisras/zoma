using TwentyTons.Tuning;

namespace TwentyTons.Core
{
    /// <summary>
    /// Physical size and plain driving limits per vehicle class. These are not gameplay tuning
    /// (a rickshaw is 2.2 m long whatever we decide about nerve), so they live here as facts.
    /// Sizes are rough Dhaka averages in metres: length (along travel), width, height.
    /// </summary>
    public struct VehicleShape
    {
        public float Length;
        public float Width;
        public float Height;
        public float CruiseSpeed;     // m/s the class likes to travel at when the road is free
        public float Acceleration;    // m/s² when pulling away
        public float Braking;         // m/s² comfortable braking
        public float LateralSpeed;    // m/s it can drift sideways while moving

        public static VehicleShape For(VehicleClass vehicleClass)
        {
            switch (vehicleClass)
            {
                case VehicleClass.Bus:
                    return new VehicleShape { Length = 11f, Width = 2.5f, Height = 3.2f, CruiseSpeed = 12f, Acceleration = 1.2f, Braking = 3.5f, LateralSpeed = 1.0f };
                case VehicleClass.Truck:
                    return new VehicleShape { Length = 8.5f, Width = 2.5f, Height = 3.0f, CruiseSpeed = 10f, Acceleration = 1.0f, Braking = 3.0f, LateralSpeed = 0.8f };
                case VehicleClass.Car:
                    return new VehicleShape { Length = 4.4f, Width = 1.8f, Height = 1.5f, CruiseSpeed = 13f, Acceleration = 2.5f, Braking = 6.0f, LateralSpeed = 1.8f };
                case VehicleClass.Cng:
                    return new VehicleShape { Length = 2.6f, Width = 1.4f, Height = 1.7f, CruiseSpeed = 11f, Acceleration = 2.0f, Braking = 4.5f, LateralSpeed = 2.0f };
                case VehicleClass.Rickshaw:
                    return new VehicleShape { Length = 2.2f, Width = 1.1f, Height = 1.8f, CruiseSpeed = 4f, Acceleration = 0.8f, Braking = 2.5f, LateralSpeed = 1.5f };
                case VehicleClass.Pedestrian:
                default:
                    return new VehicleShape { Length = 0.5f, Width = 0.5f, Height = 1.7f, CruiseSpeed = 1.3f, Acceleration = 2.0f, Braking = 4.0f, LateralSpeed = 1.3f };
            }
        }
    }
}
