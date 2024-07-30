using UnityEngine;
using RosMessageTypes.Sensor;
using Unity.Robotics.Core; //Clock

using SensorGPS = VehicleComponents.Sensors.GPS;
using VehicleComponents.ROS.Core;


namespace VehicleComponents.ROS.Publishers
{
    [RequireComponent(typeof(SensorGPS))]
    class GPS: ROSPublisher<NavSatFixMsg, SensorGPS>
    { 

        public override void UpdateMessage()
        {
            ROSMsg.header.stamp = new TimeStamp(Clock.time);
            if(sensor.fix) 
            {
                ROSMsg.status.status = NavSatStatusMsg.STATUS_FIX;
                ROSMsg.latitude = sensor.lat;
                ROSMsg.longitude = sensor.lon;
            }
            else ROSMsg.status.status = NavSatStatusMsg.STATUS_NO_FIX;
        }
    }
}