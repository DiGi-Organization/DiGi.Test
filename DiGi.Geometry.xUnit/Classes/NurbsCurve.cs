namespace DiGi.Geometry.xUnit
{
    using Math = System.Math;

    public struct Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3D operator +(Vector3D v1, Vector3D v2) => new(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);

        public static Vector3D operator -(Vector3D v1, Vector3D v2) => new(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);

        public static Vector3D operator *(Vector3D v, double scalar) => new(v.X * scalar, v.Y * scalar, v.Z * scalar);

        public static Vector3D operator /(Vector3D v, double scalar) => new(v.X / scalar, v.Y / scalar, v.Z / scalar);

        public double LengthSquared() => X * X + Y * Y + Z * Z;

        public double Length() => Math.Sqrt(LengthSquared());

        public static double Dot(Vector3D v1, Vector3D v2) => v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;

        public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
    }

    /// <summary>Represents a Non-Uniform Rational B-Spline (NURBS) curve defined by a set of control points, associated weights, a knot vector, and a degree.</summary>
    public class NurbsCurve
    {
        // (Previous properties and constructor from previous response, copy-paste here)
        /// <summary>Gets the collection of <see cref="Vector3D"/> control points that define the geometry of the NURBS curve.</summary>
        public List<Vector3D> ControlPoints { get; private set; }

        /// <summary>Gets the collection of weight values associated with each control point of the NURBS curve.</summary>
        public List<double> Weights { get; private set; }

        /// <summary>Gets the sequence of knot values that determine the parameterization of the NURBS curve.</summary>
        public List<double> KnotVector { get; private set; }

        /// <summary>Gets the degree of the NURBS curve.</summary>
        public int Degree { get; private set; }

        /// <summary>Gets the total number of control points that define the NURBS curve.</summary>
        public int NumberOfControlPoints => ControlPoints.Count;

        /// <summary>Gets the index of the last control point in the NURBS curve, calculated as the total number of control points minus one.</summary>
        public int N => NumberOfControlPoints - 1;

        /// <summary>Gets the total number of knots in the knot vector, calculated as the number of control points plus the degree plus one.</summary>
        public int M => N + Degree + 1;

        /// <summary>Initializes a new instance of the <see cref="NurbsCurve"/> class with the specified control points, weights, knot vector, and degree.</summary>
        /// <param name="controlPoints">The collection of <see cref="Vector3D"/> control points that define the geometry of the curve.</param>
        /// <param name="weights">The collection of weight values associated with each control point; must be equal in length to the <paramref name="controlPoints"/> collection.</param>
        /// <param name="knotVector">The sequence of knot values that determine the parameterization of the curve.</param>
        /// <param name="degree">The degree of the NURBS curve, which must be a positive integer of at least 1.</param>
        public NurbsCurve(IEnumerable<Vector3D> controlPoints, IEnumerable<double> weights, IEnumerable<double> knotVector, int degree)
        {
            if (controlPoints == null || !controlPoints.Any())
                throw new ArgumentException("Control points cannot be null or empty.");
            if (weights == null || !weights.Any())
                throw new ArgumentException("Weights cannot be null or empty.");
            if (knotVector == null || !knotVector.Any())
                throw new ArgumentException("Knot vector cannot be null or empty.");
            if (controlPoints.Count() != weights.Count())
                throw new ArgumentException("Number of control points and weights must be equal.");
            if (knotVector.Count() != controlPoints.Count() + degree + 1)
                throw new ArgumentException($"Knot vector size is incorrect. Expected: {controlPoints.Count() + degree + 1}, Got: {knotVector.Count()}");
            if (degree < 1)
                throw new ArgumentException("Degree must be at least 1.");

            ControlPoints = controlPoints.ToList();
            Weights = weights.ToList();
            KnotVector = knotVector.ToList();
            Degree = degree;

            for (int i = 0; i < KnotVector.Count - 1; i++)
            {
                if (KnotVector[i] > KnotVector[i + 1])
                    throw new ArgumentException("Knot vector must be non-decreasing.");
            }
        }

        // (Evaluate and FindKnotSpan from previous response, copy-paste here)
        /// <summary>Evaluates the NURBS curve at a specific parameter value to determine the corresponding 3D point on the curve.</summary>
        /// <param name="u">The parameter value along the curve, typically within the range defined by the knot vector.</param>
        /// <returns>A <see cref="Vector3D"/> representing the coordinates of the point on the curve at the specified parameter; returns a vector with NaN components if the sum of weighted basis functions is zero.</returns>
        public Vector3D Evaluate(double u)
        {
            int k = FindKnotSpan(u);
            double[] basisFunctions = CalculateNurbsBasisFunctions(k, u);

            Vector3D C = new(0, 0, 0);
            double sumBasisTimesWeights = 0;

            for (int i = 0; i <= Degree; i++)
            {
                int controlPointIndex = k - Degree + i;
                if (controlPointIndex >= 0 && controlPointIndex < NumberOfControlPoints)
                {
                    double N_i_p = basisFunctions[i];
                    double weightedBasis = N_i_p * Weights[controlPointIndex];
                    C += ControlPoints[controlPointIndex] * weightedBasis;
                    sumBasisTimesWeights += weightedBasis;
                }
            }
            return sumBasisTimesWeights == 0 ? new Vector3D(double.NaN, double.NaN, double.NaN) : C / sumBasisTimesWeights;
        }

        private int FindKnotSpan(double u)
        {
            int low = Degree;
            int high = M - Degree;

            if (u >= KnotVector[high]) return high - 1;
            if (u <= KnotVector[low]) return low;

            int k = (low + high) / 2;
            while (u < KnotVector[k] || u >= KnotVector[k + 1])
            {
                if (u < KnotVector[k]) high = k;
                else low = k;
                k = (low + high) / 2;
            }
            return k;
        }

        private double[] CalculateNurbsBasisFunctions(int k, double u)
        {
            double[] N = new double[Degree + 1];
            N[0] = 1.0;

            double[] left = new double[Degree + 1];
            double[] right = new double[Degree + 1];

            for (int j = 1; j <= Degree; j++)
            {
                left[j] = u - KnotVector[k + 1 - j];
                right[j] = KnotVector[k + j] - u;

                double saved = 0.0;
                for (int r = 0; r < j; r++)
                {
                    double temp = N[r] / (right[r + 1] + left[j - r]);
                    N[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }
                N[j] = saved;
            }
            return N;
        }

        /// <summary>
        /// Generates a clamped knot vector for a NURBS curve, ensuring that the curve starts and ends exactly at the first and last control points.
        /// </summary>
        /// <param name="numberOfControlPoints">The total number of control points used to define the curve.</param>
        /// <param name="degree">The degree of the B-spline or NURBS curve.</param>
        /// <returns>A list of doubles representing the clamped knot vector.</returns>
        public static List<double> CreateClampedKnotVector(int numberOfControlPoints, int degree)
        {
            int m = numberOfControlPoints + degree;
            List<double> knotVector = new(m + 1);

            for (int i = 0; i <= degree; i++) knotVector.Add(0.0);
            for (int i = 1; i <= numberOfControlPoints - 1 - degree; i++) knotVector.Add((double)i / (numberOfControlPoints - degree));
            for (int i = 0; i <= degree; i++) knotVector.Add(1.0);

            return knotVector;
        }

        // --- NEW: Basis Function Derivatives (needed for curve derivatives) ---
        /// <summary>
        /// Computes the B-spline basis function derivatives N_i,k(u) for a given degree and derivative order.
        /// This is an advanced version of the Cox-DeBoor algorithm.
        /// </summary>
        /// <param name="k">The knot span index.</param>
        /// <param name="u">The parameter value.</param>
        /// <param name="order">The derivative order (0 for basis function itself, 1 for first derivative, etc.)</param>
        /// <returns>A 2D array [derivative_order][basis_function_index] of derivative values.</returns>
        private double[][] CalculateNurbsBasisFunctionDerivatives(int k, double u, int order)
        {
            // Max derivative order we need is 2 (for C''(u))
            if (order > Degree) order = Degree; // Cannot compute derivative higher than degree

            double[][] ND = new double[order + 1][];
            for (int i = 0; i <= order; i++)
            {
                ND[i] = new double[Degree + 1];
            }

            double[] N = new double[Degree + 1];
            double[] left = new double[Degree + 1];
            double[] right = new double[Degree + 1];

            // Zero-th order basis functions (the basis functions themselves)
            N[0] = 1.0;
            for (int j = 1; j <= Degree; j++)
            {
                left[j] = u - KnotVector[k + 1 - j];
                right[j] = KnotVector[k + j] - u;

                double saved = 0.0;
                for (int r = 0; r < j; r++)
                {
                    double denominator = right[r + 1] + left[j - r];
                    if (Math.Abs(denominator) < 1e-9) // Handle division by zero
                    {
                        // This can happen if knots are repeated multiple times (multiplicity = degree).
                        // In such cases, the basis function might be zero or a constant.
                        // Proper handling requires careful consideration based on the specific context.
                        // For now, we'll treat it as zero.
                        N[r] = 0.0;
                        saved = 0.0;
                        continue;
                    }

                    double temp = N[r] / denominator;
                    N[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }
                N[j] = saved;
            }

            // Store the 0-th order (basis functions)
            for (int r = 0; r <= Degree; r++)
            {
                ND[0][r] = N[r];
            }

            // Higher order derivatives
            double[][] a = new double[2][];
            a[0] = new double[Degree + 1];
            a[1] = new double[Degree + 1];

            for (int r = 0; r <= Degree; r++)
            {
                ND[0][r] = N[r];
            }

            for (int s = 1; s <= order; s++)
            {
                for (int r = 0; r <= Degree - s; r++)
                {
                    a[0][r] = ND[s - 1][r];
                    a[1][r] = ND[s - 1][r + 1];
                }

                double saved = 0.0;
                for (int r = 0; r <= Degree - s; r++)
                {
                    double denominator = KnotVector[k + r + 1] - KnotVector[k + r + 1 - s];
                    if (Math.Abs(denominator) < 1e-9) // Handle division by zero
                    {
                        ND[s][r] = 0.0;
                        saved = 0.0;
                        continue;
                    }

                    double d = (double)(Degree - s + 1) / denominator;
                    double temp = a[0][r] * d;
                    ND[s][r] = saved + a[1][r] * d;
                    saved = temp;
                }
            }
            return ND;
        }

        /// <summary>
        /// Evaluates the curve's derivative at a given parameter 'u'.
        /// Returns both the curve point and its first derivative (tangent).
        /// </summary>
        /// <param name="u">The parameter value.</param>
        /// <param name="derivativeOrder">The order of the derivative to compute (1 for C', 2 for C'').</param>
        /// <returns>A tuple containing the curve point and its derivative vector.</returns>
        public (Vector3D CurvePoint, Vector3D Derivative) EvaluateWithDerivative(double u, int derivativeOrder = 1)
        {
            if (derivativeOrder < 0 || derivativeOrder > 2)
                throw new ArgumentOutOfRangeException(nameof(derivativeOrder), "Derivative order must be 0, 1, or 2 for this method.");

            int k = FindKnotSpan(u);
            double[][] ND = CalculateNurbsBasisFunctionDerivatives(k, u, derivativeOrder);

            // Compute the weighted control points for the rational formulation
            List<Vector3D> weightedControlPoints = new();
            for (int i = 0; i < NumberOfControlPoints; i++)
            {
                weightedControlPoints.Add(ControlPoints[i] * Weights[i]);
            }

            // Calculate the derivative of the curve C(u) = S(u) / W(u)
            // Where S(u) = sum(N_i,p(u) * P_i * w_i)
            // And W(u) = sum(N_i,p(u) * w_i)

            // Calculate S(u) and W(u) for the current derivative order
            Vector3D S = new(0, 0, 0);
            double W = 0;

            for (int i = 0; i <= Degree; i++)
            {
                int controlPointIndex = k - Degree + i;
                if (controlPointIndex >= 0 && controlPointIndex < NumberOfControlPoints)
                {
                    S += weightedControlPoints[controlPointIndex] * ND[0][i]; // N_i,p(u)
                    W += Weights[controlPointIndex] * ND[0][i];               // N_i,p(u)
                }
            }

            // Handle the case where W is zero, which can happen with invalid knot vectors or u values
            if (Math.Abs(W) < 1e-9)
            {
                // This is a problematic state. Return NaN or throw an exception.
                return (new Vector3D(double.NaN, double.NaN, double.NaN), new Vector3D(double.NaN, double.NaN, double.NaN));
            }

            Vector3D C_u = S / W; // The curve point itself

            if (derivativeOrder == 0) return (C_u, new Vector3D(0, 0, 0)); // No derivative needed

            // Calculate S'(u) and W'(u) for the first derivative
            Vector3D S_prime = new(0, 0, 0);
            double W_prime = 0;
            for (int i = 0; i <= Degree; i++)
            {
                int controlPointIndex = k - Degree + i;
                if (controlPointIndex >= 0 && controlPointIndex < NumberOfControlPoints)
                {
                    S_prime += weightedControlPoints[controlPointIndex] * ND[1][i]; // N'_i,p(u)
                    W_prime += Weights[controlPointIndex] * ND[1][i];               // N'_i,p(u)
                }
            }

            // C'(u) = (S'(u) * W(u) - S(u) * W'(u)) / W(u)^2
            Vector3D C_prime_u = (S_prime * W - S * W_prime) / (W * W);

            if (derivativeOrder == 1) return (C_u, C_prime_u);

            // Calculate S''(u) and W''(u) for the second derivative
            Vector3D S_double_prime = new(0, 0, 0);
            double W_double_prime = 0;
            for (int i = 0; i <= Degree; i++)
            {
                int controlPointIndex = k - Degree + i;
                if (controlPointIndex >= 0 && controlPointIndex < NumberOfControlPoints)
                {
                    S_double_prime += weightedControlPoints[controlPointIndex] * ND[2][i]; // N''_i,p(u)
                    W_double_prime += Weights[controlPointIndex] * ND[2][i];               // N''_i,p(u)
                }
            }

            // C''(u) = (S''(u)W(u) - S(u)W''(u) - 2S'(u)W'(u)) / W(u)^2 + 2S(u)W'(u)^2 / W(u)^3
            // A more stable form:
            // C''(u) = (S''(u)W(u) - S(u)W''(u) - 2 * W'(u) * C'(u)) / W(u)
            Vector3D C_double_prime_u = (S_double_prime * W - S * W_double_prime - C_prime_u * (2 * W_prime)) / W;

            return (C_u, C_prime_u); // We only need C_u and C_prime_u for the closest point
        }

        /// <summary>
        /// Finds the parameter 'u' on the curve that is closest to a given query point.
        /// Uses Newton's method.
        /// </summary>
        /// <param name="queryPoint">The 3D point to find the closest point to.</param>
        /// <param name="tolerance">The desired precision for the 'u' parameter.</param>
        /// <param name="maxIterations">Maximum number of iterations for Newton's method.</param>
        /// <returns>The parameter 'u' of the closest point, or NaN if no solution is found.</returns>
        public double ClosestPoint(Vector3D queryPoint, double tolerance = 1e-6, int maxIterations = 100)
        {
            // Initial guess for 'u'. A good starting guess can significantly improve performance.
            // For simplicity, we'll start in the middle of the valid parameter range.
            // A more robust approach might evaluate at several points and pick the closest one.
            double u_min = KnotVector[Degree];
            double u_max = KnotVector[M - Degree];
            double u = (u_min + u_max) / 2.0;

            for (int i = 0; i < maxIterations; i++)
            {
                var (C_u, C_prime_u) = EvaluateWithDerivative(u, 1);

                // Handle cases where derivatives are NaN (e.g., at knot multiplicities)
                if (double.IsNaN(C_u.X) || double.IsNaN(C_prime_u.X))
                {
                    // Try a small perturbation or a different initial guess
                    u += 1e-5; // Small step
                    u = Math.Max(u_min, Math.Min(u_max, u)); // Keep within bounds
                    if (i > maxIterations / 2) // If still failing, give up
                        return double.NaN;
                    continue;
                }

                Vector3D P_minus_C = queryPoint - C_u;
                double f_u = Vector3D.Dot(C_prime_u, P_minus_C);

                if (Math.Abs(f_u) < tolerance)
                {
                    // Check if the solution is within the valid parameter range
                    return Math.Max(u_min, Math.Min(u_max, u));
                }

                // Calculate the second derivative C''(u) for f'(u)
                // C''(u) is needed for the derivative of the objective function f'(u).
                // We need to re-evaluate EvaluateWithDerivative for order 2 for C_double_prime_u.
                // For efficiency, you could modify EvaluateWithDerivative to return C_u, C_prime_u, and C_double_prime_u at once.
                var (_, C_prime_from_double_prime, C_double_prime_u) = EvaluateWithDerivativeForSecondDerivative(u); // Special call for C''

                double f_prime_u = Vector3D.Dot(C_double_prime_u, P_minus_C) - Vector3D.Dot(C_prime_u, C_prime_u);

                if (Math.Abs(f_prime_u) < 1e-9) // Avoid division by zero
                {
                    // The derivative of the objective function is zero.
                    // This means we might be at a local extremum, or it's a flat region.
                    // Newton's method won't converge. We could try a small step or break.
                    return double.NaN;
                }

                double delta_u = f_u / f_prime_u;
                u -= delta_u;

                // Clamp u to the valid parameter range to prevent divergence
                u = Math.Max(u_min, Math.Min(u_max, u));
            }

            // If it didn't converge within maxIterations, return NaN
            return double.NaN;
        }

        /// <summary>
        /// Helper to evaluate curve point, first derivative, and second derivative.
        /// This is a private helper to avoid recomputing common parts.
        /// </summary>
        private (Vector3D CurvePoint, Vector3D FirstDerivative, Vector3D SecondDerivative) EvaluateWithDerivativeForSecondDerivative(double u)
        {
            int k = FindKnotSpan(u);
            double[][] ND = CalculateNurbsBasisFunctionDerivatives(k, u, 2); // Get up to 2nd derivative of basis functions

            List<Vector3D> weightedControlPoints = new();
            for (int i = 0; i < NumberOfControlPoints; i++)
            {
                weightedControlPoints.Add(ControlPoints[i] * Weights[i]);
            }

            // W(u) and S(u)
            Vector3D S = new(0, 0, 0);
            double W = 0;
            for (int i = 0; i <= Degree; i++)
            {
                int controlPointIndex = k - Degree + i;
                if (controlPointIndex >= 0 && controlPointIndex < NumberOfControlPoints)
                {
                    S += weightedControlPoints[controlPointIndex] * ND[0][i];
                    W += Weights[controlPointIndex] * ND[0][i];
                }
            }
            if (Math.Abs(W) < 1e-9) return (new Vector3D(double.NaN, double.NaN, double.NaN), new Vector3D(double.NaN, double.NaN, double.NaN), new Vector3D(double.NaN, double.NaN, double.NaN));
            Vector3D C_u = S / W;

            // W'(u) and S'(u)
            Vector3D S_prime = new(0, 0, 0);
            double W_prime = 0;
            for (int i = 0; i <= Degree; i++)
            {
                int controlPointIndex = k - Degree + i;
                if (controlPointIndex >= 0 && controlPointIndex < NumberOfControlPoints)
                {
                    S_prime += weightedControlPoints[controlPointIndex] * ND[1][i];
                    W_prime += Weights[controlPointIndex] * ND[1][i];
                }
            }
            Vector3D C_prime_u = (S_prime * W - S * W_prime) / (W * W);

            // W''(u) and S''(u)
            Vector3D S_double_prime = new(0, 0, 0);
            double W_double_prime = 0;
            for (int i = 0; i <= Degree; i++)
            {
                int controlPointIndex = k - Degree + i;
                if (controlPointIndex >= 0 && controlPointIndex < NumberOfControlPoints)
                {
                    S_double_prime += weightedControlPoints[controlPointIndex] * ND[2][i];
                    W_double_prime += Weights[controlPointIndex] * ND[2][i];
                }
            }
            Vector3D C_double_prime_u = (S_double_prime * W - S * W_double_prime - C_prime_u * (2 * W_prime)) / W;

            return (C_u, C_prime_u, C_double_prime_u);
        }
    }
}