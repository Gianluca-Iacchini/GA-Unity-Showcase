using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

public class GaussianDistribution
{

    private static System.Random random = new();
    

    /// <summary>
    /// Computes a random number from a Gaussian distribution with the given mean and standard deviation.
    /// </summary>
    /// <param name="mean">Mean of the distribution</param>
    /// <param name="standardDeviation">Standard deviation of the distribution</param>
    /// <returns></returns>
    public static float GenerateRandomGaussian(float mean=0f, float standardDeviation=1f)
    {
        double u1 = 1.0 - random.NextDouble(); // Uniform random variables
        double u2 = 1.0 - random.NextDouble();

        double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        // Scale and shift the value to match the desired mean and standard deviation
        return (float)(mean + z0 * standardDeviation);
    }

    /// <summary>
    /// Computes a random number from 0 to N (exclusive) with a probability skewed towards choosing the higher numbers.
    /// </summary>
    /// <param name="N"></param>
    /// <returns></returns>
    public static int RandomlySelectElement(int N)
    {
        double[] probabilities = Enumerable.Range(0, N).Select((_, i) => Math.Pow(2, i + 1)).ToArray();
        double sum = probabilities.Sum();

        double randomValue = new System.Random().NextDouble() * sum;
        double cumulativeProbability = 0;

        for (int i = 0; i < N; i++)
        {
            cumulativeProbability += probabilities[i];

            if (randomValue <= cumulativeProbability)
            {
                return i;
            }
        }

        return Math.Max(N - 1, 0);
    }
}
