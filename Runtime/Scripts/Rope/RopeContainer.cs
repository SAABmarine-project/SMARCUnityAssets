using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Rope
{
    public class RopeContainer : MonoBehaviour
    {
        public GameObject RopeLinkPrefab;
        public GameObject BuoyPrefab;

        [Tooltip("What should the first link in the rope connect to?")]
        public ArticulationBody ConnectedAB;
        public Rigidbody ConnectedRB;


        [Tooltip("Diameter of the rope in meters")]
        public float RopeDiameter = 0.01f;
        [Tooltip("Diameter of the collision objects for the rope. The bigger the more stable the physics are.")]
        public float RopeCollisionDiameter = 0.1f;
        [Tooltip("How long each segment of the rope will be. Smaller = more realistic but harder to simulate.")]
        [Range(0.01f, 1f)]
        public float SegmentLength = 0.1f;

        [Tooltip("How long the entire rope should be. Rounded to SegmentLength. Ignored if this is not the root of the rope.")]
        public float RopeLength = 1f;
        public int numSegments;

        void OnValidate()
        {
            numSegments = (int)(RopeLength / (SegmentLength-RopeDiameter));
            if(numSegments > 30) Debug.LogWarning($"There will be {numSegments} rope segments generated on game Start, might be too many?");
        }

        GameObject InstantiateLink(GameObject prevLink, int num, GameObject prefab, bool buoy=false)
        {
            var link = Instantiate(prefab);
            link.transform.SetParent(transform);
            if(!buoy) link.name = $"RopeLink_{num}";

            var linkJoint = link.GetComponent<Joint>();
            if(prevLink != null)
            {
                var linkZ = prevLink.transform.localPosition.z + (SegmentLength-RopeDiameter/2);
                link.transform.localPosition = new Vector3(0, 0, linkZ);
                link.transform.rotation = prevLink.transform.rotation;
                linkJoint.connectedBody = prevLink.GetComponent<Rigidbody>();
            }
            else
            {
                if(ConnectedAB != null)
                    linkJoint.connectedArticulationBody = ConnectedAB;
                else
                    linkJoint.connectedBody = ConnectedRB;
                link.transform.localPosition = Vector3.zero;
                link.transform.rotation = transform.rotation;
            }

            var rl = link.GetComponent<RopeLink>();
            if(!buoy) rl.SetRopeSizes(RopeDiameter, RopeCollisionDiameter, SegmentLength);
            rl.SetupJoint();

            return link;
        }


        public void SpawnRope()
        {
            var links = new GameObject[numSegments];

            links[0] = InstantiateLink(null, 0, RopeLinkPrefab);

            for(int i=1; i < numSegments; i++)
            {
                links[i] = InstantiateLink(links[i-1], i, RopeLinkPrefab);
            }

            if(BuoyPrefab != null)
                InstantiateLink(links[numSegments-1], numSegments, BuoyPrefab, true);
        }

        public void DestroyRope()
        {
            for(int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
    }
}