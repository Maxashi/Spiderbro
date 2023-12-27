
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public struct SpiderLeg
{
    public Transform legTarget;

    public Vector3 desiredLegPosition;
    public Vector3 lastLegPosition;
    public Vector3 defaultLegPosition;
    public bool isMoving;

}


