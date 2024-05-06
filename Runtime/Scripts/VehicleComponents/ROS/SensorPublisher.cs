using UnityEngine;

using Unity.Robotics.Core; //Clock
using Unity.Robotics.ROSTCPConnector;

using Sensor = VehicleComponents.Sensors.Sensor;
using ISensor = VehicleComponents.Sensors.ISensor;

namespace VehicleComponents.ROS
{
    [RequireComponent(typeof(Sensor))]
    public class SensorPublisher<RosMsgType, SensorType> : MonoBehaviour
        where RosMsgType: Unity.Robotics.ROSTCPConnector.MessageGeneration.Message, new()
        where SensorType: ISensor
    {
        ROSConnection ros;
        float frequency = 10f;
        float period => 1.0f/frequency;
        double lastTime;

        // Subclasses should be able to access these
        // to get data from the sensor and put it in
        // ROSMsg as needed.
        protected SensorType sensor;
        protected RosMsgType ROSMsg;

        [Header("ROS Publisher")]
        [Tooltip("The topic will be namespaced under the root objects name if the given topic does not start with '/'.")]
        public string topic;


        void Awake()
        {
            // We namespace the topics with the root name
            if(topic[0] != '/') topic = $"/{transform.root.name}/{topic}";

            sensor = GetComponent<SensorType>();
            frequency = sensor.Frequency();
            ROSMsg = new RosMsgType();

            ros = ROSConnection.GetOrCreateInstance();
            ros.RegisterPublisher<RosMsgType>(topic);
            lastTime = Clock.NowTimeInSeconds;
        }

        public virtual void UpdateMessage()
        {
            Debug.Log($"The SensorPublisher with topic {topic} did not override the UpdateMessage method!");
        }

        void FixedUpdate()
        {
            var deltaTime = Clock.NowTimeInSeconds - lastTime;
            if(deltaTime < period) return;
            
            // If the underlying sensor does not have new data
            // do not publish anything.
            if(sensor.HasNewData())
            {
                UpdateMessage();
                ros.Publish(topic, ROSMsg);
                lastTime = Clock.NowTimeInSeconds;
            }
        }
    }

}