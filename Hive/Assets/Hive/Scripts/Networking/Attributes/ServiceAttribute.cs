using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameData.Networking.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceAttribute : Attribute
    {
    }

    public class RequestAttribute : Attribute
    {
        
    }

    public class ResponseAttribute : Attribute
    {
        
    }
}