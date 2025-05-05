using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rope : MonoBehaviour
{
    public Rigidbody2D hook;
    public GameObject[] prefabRopeSegments;
    public int numLinks = 5;

    void Start()
    {
        GenerateRope();
    } 

    void GenerateRope() {
        Rigidbody2D prevBod = hook;
        for (int i = 0; i < numLinks; i++) {
            int index = Random.Range(0, prefabRopeSegments.Length);
            GameObject newSegment = Instantiate(prefabRopeSegments[index]);
            newSegment.transform.parent = transform;
            newSegment.transform.position = transform.position;
            HingeJoint2D hj = newSegment.GetComponent<HingeJoint2D>();
            hj.connectedBody = prevBod;

            prevBod = newSegment.GetComponent<Rigidbody2D>();
        }
    }

}
