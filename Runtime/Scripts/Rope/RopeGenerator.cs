using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using Utils = DefaultNamespace.Utils;

namespace Rope
{
    public class RopeGenerator : MonoBehaviour
    {
        [Header("Prefab of the rope parts")]
        public GameObject RopeLinkPrefab;
        public GameObject BuoyPrefab;

        [Header("Connected Body")]
        [Tooltip("What should the first link in the rope connect to? rope_link in SAM.")]
        public string VehicleRopeLinkName = "rope_link";
        public  string VehicleBaseLinkName = "base_link";
    

        [Header("Rope parameters")]
        [Tooltip("Diameter of the rope in meters")]
        public float RopeDiameter = 0.01f;
        [Tooltip("How long the entire rope should be. Rounded to SegmentLength. Ignored if this is not the root of the rope.")]
        public float RopeLength = 1f;
        [Tooltip("How heavy is this rope?")]
        public float GramsPerMeter = 0.5f;
        [Tooltip("How heavy is the buoy at the end. Set to 0 for no buoy.")]
        public float BuoyGrams = 0f;

        [Header("Physics stuff")]
        [Tooltip("Diameter of the collision objects for the rope. The bigger the more stable the physics are.")]
        public float RopeCollisionDiameter = 0.1f;
        [Tooltip("How long each segment of the rope will be. Smaller = more realistic but harder to simulate.")]
        [Range(0.01f, 1f)]
        public float SegmentLength = 0.1f;
        [Tooltip("Rope will be replaced by two sticks when its end-to-end distance is this close to RopeLength")]
        [Range(0f, 0.05f)]
        public float RopeTightnessTolerance = 0.02f;

        [Header("Rendering")]
        public Color RopeColor = Color.yellow;
        LineRenderer ropeLineRenderer;

        [Header("Debug")]
        public bool DrawGizmos = false;

        [HideInInspector] public float SegmentMass => GramsPerMeter * 0.001f * SegmentLength;
        [HideInInspector] public int NumSegments => (int)(RopeLength / (SegmentLength+RopeDiameter));
        //All the rope links we generate will go in here
        [HideInInspector] public GameObject RopeContainer;


        [HideInInspector] public GameObject VehicleRopeLink;
        [HideInInspector] public GameObject VehicleBaseLink;
        readonly string containerName = "Rope";
        

        void OnValidate()
        {
            if(NumSegments > 50) Debug.LogWarning($"There will be {NumSegments} rope segments generated on game Start, might be too many?");
        }

        GameObject InstantiateLink(Transform prevLink, int num)
        {
            var link = Instantiate(RopeLinkPrefab);
            link.transform.SetParent(RopeContainer.transform);
            link.name = $"{link.name}_{num}";

            var rl = link.GetComponent<RopeLink>();
            rl.SetRopeParams(this);

            if(prevLink != null) rl.SetupConnectionToPrevLink(prevLink);
            else rl.SetupConnectionToVehicle(VehicleRopeLink, VehicleBaseLink);
            
            return link;
        }


        public void SpawnRope()
        {
            VehicleRopeLink = Utils.FindDeepChildWithName(transform.root.gameObject, VehicleRopeLinkName);
            VehicleBaseLink = Utils.FindDeepChildWithName(transform.root.gameObject, VehicleBaseLinkName);
            
            RopeContainer = Utils.FindDeepChildWithName(transform.root.gameObject, containerName);
            if(RopeContainer == null)
            {
                RopeContainer = new GameObject(containerName);
                RopeContainer.transform.SetParent(transform.root);
                RopeContainer.transform.position = VehicleRopeLink.transform.position;
                RopeContainer.transform.rotation = VehicleRopeLink.transform.rotation;
            }

            ropeLineRenderer = RopeContainer.AddComponent<LineRenderer>();
            ropeLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            ropeLineRenderer.startColor = RopeColor;
            ropeLineRenderer.endColor = RopeColor;
            ropeLineRenderer.startWidth = RopeDiameter;
            ropeLineRenderer.endWidth = RopeDiameter;
            ropeLineRenderer.positionCount = NumSegments+1;
            ropeLineRenderer.useWorldSpace = true;
            ropeLineRenderer.receiveShadows = false;

            InstantiateLink(null, 0);

            for(int i=1; i < NumSegments; i++)
            {
                InstantiateLink(RopeContainer.transform.GetChild(i-1), i);
            }

            foreach(RopeLink rl in RopeContainer.GetComponentsInChildren<RopeLink>())
            {
                rl.AssignFirstAndLastSegments();
            }

            UpdateLineRenderer();

            if(BuoyGrams > 0 && BuoyPrefab != null)
            {
                var lastLink = RopeContainer.transform.GetChild(NumSegments-1);
                var lastLinkRL = lastLink.GetComponent<RopeLink>();
                var (lastLinkFront, lastLinkBack) = lastLinkRL.SpherePositions();
                var buoy = Instantiate(BuoyPrefab);
                buoy.transform.SetParent(RopeContainer.transform);
                buoy.transform.position = lastLink.position + lastLinkFront;
                buoy.transform.rotation = lastLink.rotation;
                buoy.name = "Buoy";
                var buoyRB = buoy.GetComponent<Rigidbody>();
                buoyRB.mass = BuoyGrams * 0.001f;
                var buoyJoint = buoy.GetComponent<CharacterJoint>();
                buoyJoint.connectedBody = lastLink.GetComponent<Rigidbody>();
            }

        }

        void DestroyEitherWay(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        public void DestroyRope(bool keepBuoy = false)
        {
            RopeContainer = Utils.FindDeepChildWithName(transform.root.gameObject, containerName);
            if(RopeContainer == null) return;

            if(!keepBuoy) DestroyEitherWay(RopeContainer);
            else
            {
                foreach(RopeLink rl in RopeContainer.GetComponentsInChildren<RopeLink>())
                {
                    Destroy(rl.gameObject);
                }
                Destroy(ropeLineRenderer);
                ropeLineRenderer = null;
            }
        }


        void Awake()
        {
            if(RopeContainer == null) RopeContainer = Utils.FindDeepChildWithName(transform.root.gameObject, containerName);
            if(VehicleRopeLink == null) VehicleRopeLink = Utils.FindDeepChildWithName(transform.root.gameObject, VehicleRopeLinkName);
            if(VehicleBaseLink == null) VehicleBaseLink = Utils.FindDeepChildWithName(transform.root.gameObject, VehicleBaseLinkName);
            if(RopeContainer != null && ropeLineRenderer == null) ropeLineRenderer = RopeContainer.GetComponent<LineRenderer>();
        }


        void UpdateLineRenderer()
        {
            if(ropeLineRenderer == null) return;
            var numPts = Mathf.Min(NumSegments, ropeLineRenderer.positionCount);
            for (int i = 0; i < numPts; i++)
            {
                var child = RopeContainer.transform.GetChild(i);
                ropeLineRenderer.SetPosition(i, child.position);
            }

            var lastChild = RopeContainer.transform.GetChild(NumSegments-1);;
            ropeLineRenderer.SetPosition(NumSegments, lastChild.position+lastChild.forward*SegmentLength);
        }


        void Update()
        {
            if(RopeContainer != null) UpdateLineRenderer();
        }

    }
}