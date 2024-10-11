using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO; // For file operations
using Utils = DefaultNamespace.Utils;
using NormalDistribution  = DefaultNamespace.NormalDistribution;
using DefaultNamespace.Water;

namespace VehicleComponents.Sensors
{
    public class DepthSensor : Sensor
    {
        [Header("Depth-Sensor")]
        public float depth;
    
        private bool headerWritten = false;
        //Noise params and generator
        public float noiseMean = 0f;
        public float noiseSigma = 0.1f;
        private NormalDistribution noiseGenerator;
        public float maxRaycastDistance = 30f;
        private WaterQueryModel _waterModel;

        void Start()
        {
            _waterModel = FindObjectsByType<WaterQueryModel>(FindObjectsSortMode.None)[0];
            depth = 0f;
            noiseGenerator = new NormalDistribution(noiseMean, noiseSigma);
        }

        public override bool UpdateSensor(double deltaTime)
        {
            RaycastHit hit;

            Vector3 rayOrigin = transform.position;
            Vector3 rayDirection = Vector3.down;

            // Perform raycast downwards from the current position
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, maxRaycastDistance))
            {
                depth = -(hit.point.y - transform.position.y);
            }
            else
            {
                // If no hit, fall back to water level calculation
                float waterSurfaceLevel = _waterModel.GetWaterLevelAt(transform.position);
                // Debug.Log("y: " + transform.position.y);
                depth = -(waterSurfaceLevel - transform.position.y);
            }
            //Add gaussian noise
            float noise = (float)noiseGenerator.Sample();
            depth = depth*(1 + noise);
            return true;
        } 
    }
}
