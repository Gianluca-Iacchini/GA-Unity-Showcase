using UnityEngine;
using System;

public class GaussianDistribution
{

    private static System.Random random = new();
    

    public static float GenerateRandomGaussian(float mean=0f, float standardDeviation=1f)
    {
        double u1 = 1.0 - random.NextDouble(); // Uniform random variables
        double u2 = 1.0 - random.NextDouble();

        double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        // Scale and shift the value to match the desired mean and standard deviation
        return (float)(mean + z0 * standardDeviation);
    }
}
