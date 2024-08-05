using LearningFoundation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoCortex;
using NeoCortexApi;
using NeoCortexApi.Encoders;
using NeoCortexApi.Entities;
using NeoCortexApi.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace WorkingWithSDR
{
    public class Program
    {
        public static void Main()
        {
            /// User can directly compair two scalar values between 0 to 99.

            //var outFolder = @"EncoderOutputImages\ScalerEncoderOutput";

            ////int[] d = new int[] { 1, 4, 5, 7, 8, 9 };     
            //Console.WriteLine("Welcome to the SDR Representation project. Please enter two numbers (0-99) to find SDR as Indices and Text, Bitmaps, Overlap, Union and Intersection");
            //Console.Write("Please enter First Number: ");
            //int ch1 = Convert.ToInt16(Console.ReadLine());
            //Console.Write("Please enter Second Number: ");
            //int ch2 = Convert.ToInt16(Console.ReadLine());


            //int[] d = new int[] { ch1, ch2 };
            //ScalarEncoderTest(d, ch1, ch2);

            //Directory.CreateDirectory(outFolder);

            //Console.WriteLine("SDR Representation using ScalarEncoder");


            //for (int input = 1; input < (int)6; input++)
            //{
            //    //double input = 1.10;


            //    ScalarEncoder encoder = new ScalarEncoder(new Dictionary<string, object>()
            //        {
            //            { "W", 25},
            //            { "N", (int)0},
            //            { "Radius", (double)2.5},
            //            { "MinVal", (double)1},
            //            { "MaxVal", (double)50},
            //            { "Periodic", false},
            //            { "Name", "Scalar Encoder"},
            //            { "ClipInput", false},
            //        });

            //    var result = encoder.Encode(input);
            //    Debug.WriteLine($"Input = {input}");
            //    Debug.WriteLine($"SDRs Generated = {NeoCortexApi.Helpers.StringifyVector(result)}");
            //    Debug.WriteLine($"SDR As Indices = {NeoCortexApi.Helpers.StringifyVector(ArrayUtils.IndexWhere(result, k => k == 1))}");

            //    int[,] twoDimenArray = ArrayUtils.Make2DArray<int>(result, (int)Math.Sqrt(result.Length), (int)Math.Sqrt(result.Length));
            //    var twoDimArray = ArrayUtils.Transpose(twoDimenArray);

            //    NeoCortexUtils.DrawBitmap(twoDimArray, 1024, 1024, $"{outFolder}\\{input}.png", Color.Yellow, Color.Black, text: input.ToString());

            //}

            Console.WriteLine("Welcome to the SDR Representation project.");
            var a = DateTime.Now;
            Console.WriteLine(a);

            Encoder_Bitmap eb = new Encoder_Bitmap();
            Console.WriteLine(" ------------------  Scalar Encoder with Similarity Results ------------------------- ");
            eb.CreateSdrAsTextTest();               // Scaler Encoder function

            Console.WriteLine(" ------------------  Date Time Encoder with Similarity Results ------------------------- ");
            eb.CreateSdrAsBitmapTest();              // date time function

            Console.WriteLine(" ------------------  Category Encoder with Similarity Results ------------------------- ");
            //string[] stringArray = new string[] { "Milk", "Suger", "Egg", "Bread" };
            eb.CategoryEncoderTest();             // Category Encoder function

            Console.WriteLine(" ------------------  Spatial Pooler results with Similarity Results ------------------------- ");
            Program p = new Program();           // SP function
            p.CreateSdrsTest();

            var b = DateTime.Now;
            Console.WriteLine(b);

            Console.WriteLine(b-a);
        }

        private const int OutImgSize = 1024;

        //[TestMethod]
        public void CreateSdrsTest()
        {
            var colDims = new int[] { 64, 64 };
            int numOfCols = 64 * 64;

            string trainingFolder = @"TestFiles\Sdr";

            int imgSize = 28;

            //var trainingImages = Directory.GetFiles(trainingFolder, "*.jpeg");

            var jpgFiles = Directory.GetFiles(trainingFolder, "*.jpg");             
            var jpegFiles = Directory.GetFiles(trainingFolder, "*.jpeg");
            var trainingImages = jpgFiles.Concat(jpegFiles).ToArray();          //take both jpg and jpeg file formates


            Directory.CreateDirectory($"{nameof(CreateSdrsTest)}");

            int counter = 0;

            bool isInStableState = false;

            // HTM parameters
            HtmConfig htmConfig = new HtmConfig(new int[] { imgSize, imgSize }, colDims)
            {
                PotentialRadius = 10,
                PotentialPct = 1,
                GlobalInhibition = true,
                LocalAreaDensity = -1.0,
                NumActiveColumnsPerInhArea = 0.02 * numOfCols,
                StimulusThreshold = 0.0,
                SynPermInactiveDec = 0.008,
                SynPermActiveInc = 0.05,
                SynPermConnected = 0.10,
                MinPctOverlapDutyCycles = 1.0,
                MinPctActiveDutyCycles = 0.001,
                DutyCyclePeriod = 100,
                MaxBoost = 10.0,
                RandomGenSeed = 42,
                Random = new ThreadSafeRandom(42)

            };

            Connections connections = new Connections(htmConfig);

            HomeostaticPlasticityController hpa = new HomeostaticPlasticityController(connections, trainingImages.Length * 50, (isStable, numPatterns, actColAvg, seenInputs) =>
            {
                isInStableState = true;

                Debug.WriteLine($"Entered STABLE state: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");
                Console.WriteLine($"Entered STABLE state: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");
                
            });

            SpatialPooler sp = new SpatialPoolerMT(hpa);

            sp.Init(connections);

            string outFolder = nameof(CreateSdrsTest);
            Directory.CreateDirectory(outFolder);

            while (true)
            {
                Console.WriteLine($"cycle - {counter}");
                counter++;

                Dictionary<string, int[]> sdrs = new Dictionary<string, int[]>();

                Dictionary<string, int[]> inputVectors = new Dictionary<string, int[]>();

                foreach (var trainingImage in trainingImages)
                {
                    FileInfo fI = new FileInfo(trainingImage);

                    string outputHamDistFile = $"{outFolder}\\image-{fI.Name}_hamming.txt";
                    string outputActColFile = $"{outFolder}\\image{fI.Name}_activeCol.txt";
                    string outputActColFile1 = $"{outFolder}\\image{fI.Name}_activeCol.csv";

                    using (StreamWriter swActCol = new StreamWriter(outputActColFile))
                    {
                        using (StreamWriter swActCol1 = new StreamWriter(outputActColFile1))
                        {
                            int[] activeArray = new int[numOfCols];

                            string testName = $"{outFolder}\\{fI.Name}";

                            string inputBinaryImageFile = NeoCortexUtils.BinarizeImage($"{trainingImage}", imgSize, testName);

                            // Read input csv file into array
                            int[] inputVector = NeoCortexUtils.ReadCsvIntegers(inputBinaryImageFile).ToArray();

                            List<double[,]> overlapArrays = new List<double[,]>();

                            List<double[,]> bostArrays = new List<double[,]>();

                            sp.compute(inputVector, activeArray, true);

                            var activeCols = ArrayUtils.IndexWhere(activeArray, (el) => el == 1);

                            if (isInStableState)
                            {
                                CalculateResult(sdrs, inputVectors, numOfCols, activeCols, outFolder, trainingImage, inputVector);

                                overlapArrays.Add(ArrayUtils.Make2DArray<double>(ArrayUtils.ToDoubleArray(connections.Overlaps), colDims[0], colDims[1]));

                                bostArrays.Add(ArrayUtils.Make2DArray<double>(connections.BoostedOverlaps, colDims[0], colDims[1]));

                                var activeStr = Helpers.StringifyVector(activeArray);

                                Debug.WriteLine($"SDR As Indices = {NeoCortexApi.Helpers.StringifyVector(ArrayUtils.IndexWhere(activeArray, k => k == 1))}"); // SDR as indicies

                                swActCol.WriteLine("Active Array: " + activeStr);

                                int[,] twoDimenArray = ArrayUtils.Make2DArray<int>(activeArray, colDims[0], colDims[1]);
                                twoDimenArray = ArrayUtils.Transpose(twoDimenArray);
                                List<int[,]> arrays = new List<int[,]>();
                                arrays.Add(twoDimenArray);
                                arrays.Add(ArrayUtils.Transpose(ArrayUtils.Make2DArray<int>(inputVector, (int)Math.Sqrt(inputVector.Length), (int)Math.Sqrt(inputVector.Length))));

                                //Calculating the max value of the overlap in the OverlapArray
                                int max = SdrRepresentation.TraceColumnsOverlap(overlapArrays, swActCol1, fI.Name);

                                int red = Convert.ToInt32(max * 0.80);        // Value above this threshould would be red and below this will be yellow 
                                int green = Convert.ToInt32(max * 0.50);      // Value above this threshould would be yellow and below this will be green

                                string outputImage = $"{outFolder}\\cycle-{counter}-{fI.Name}";

                                NeoCortexUtils.DrawBitmaps(arrays, outputImage, Color.Yellow, Color.Gray, OutImgSize, OutImgSize);
                                NeoCortexUtils.DrawHeatmaps(overlapArrays, $"{outputImage}_overlap.png", 1024, 1024, red, red, green);

                                if (sdrs.Count == trainingImages.Length)
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }


        // <summary>
        /// Calculate all required results.
        /// 1. Overlap and Union of the Spatial Pooler SDRs of two Images as Input
        ///    It cross compares the 1st SDR with it self and all the Tranings Images.
        /// 2. Creates bitmaps of the overlaping and non-overlaping regions of the Comparing SDRs.
        /// 3. Also generate HeatMaps of the SDRs during Spatial Pooler learning Phase.
        /// </summary>
        /// <param name="sdrs"></param>
        private void CalculateResult(Dictionary<string, int[]> sdrs, Dictionary<string, int[]> inputVectors, int numOfCols, int[] activeCols, string outFolder, string trainingImage, int[] inputVector)
        {
            int[] CompareArray = new int[numOfCols];
            int[] ActiveArray = new int[numOfCols];

            ActiveArray = SdrRepresentation.GetIntArray(activeCols, 4096);

            sdrs.Add(trainingImage, activeCols);
            inputVectors.Add(trainingImage, inputVector);
            int[] FirstSDRArray = new int[81];
            if (sdrs.First().Key == null)
            {
                FirstSDRArray = new int[sdrs.First().Value.Length];

            }

            FirstSDRArray = sdrs.First().Value;

            CompareArray = SdrRepresentation.GetIntArray(FirstSDRArray, 4096);

            var Array = SdrRepresentation.OverlapArraFun(ActiveArray, CompareArray);
            int[,] twoDimenArray2 = ArrayUtils.Make2DArray<int>(Array, (int)Math.Sqrt(Array.Length), (int)Math.Sqrt(Array.Length));
            int[,] twoDimArray1 = ArrayUtils.Transpose(twoDimenArray2);
            NeoCortexUtils.DrawBitmap(twoDimArray1, 1024, 1024, $"{outFolder}\\Overlap_{sdrs.Count}.png", Color.PaleGreen, Color.Red, text: $"Overlap.png");

            //Array = ActiveArray.Union(CompareArray).ToArray();
            var Array1 = Union(ActiveArray, CompareArray).ToArray();
            int[,] twoDimenArray4 = ArrayUtils.Make2DArray<int>(Array1, (int)Math.Sqrt(Array.Length), (int)Math.Sqrt(Array.Length));
            int[,] twoDimArray3 = ArrayUtils.Transpose(twoDimenArray4);
            NeoCortexUtils.DrawBitmap(twoDimArray3, 1024, 1024, $"{outFolder}\\Union_{sdrs.Count}.png", Color.PaleGreen, Color.Green, text: $"Union.png");

            // Bitmap Intersection Image of two bit arrays selected for comparison
            SdrRepresentation.DrawIntersections(twoDimArray3, twoDimArray1, 10, $"{outFolder}\\Intersection_{sdrs.Count}.png", Color.Black, Color.Gray, text: $"Intersection.png");

            return;
        }

         /// <summary>
        /// Vaildate method <see cref="SdrRepresentation.OverlapArraFun(int[], int[])"/>
        /// </summary>
        [TestMethod]
        public void OverlapArraFunTest()
        {
            int[] a1 = new int[] { 1, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1 };
            int[] a2 = new int[] { 0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 0 };

            Assert.ThrowsException<IndexOutOfRangeException>(() => SdrRepresentation.OverlapArraFun(a1, a2));
            var res = SdrRepresentation.OverlapArraFun(a2, a1);

            Assert.IsNotNull(res);
        }

        public static int[] Union(int[] arr1, int[] arr2)                        // To find union of of the Binary arrays of two scalar values.
        {


            int[] union = new int[arr1.Length];

            for (int i = 0; i < arr1.Length; i++)
            {

                if (arr1[i] == 0 && arr2[i] == 0)
                {
                    union[i] = 0;
                }
                else
                {
                    union[i] = 1;
                }
            }
            return union;
        }


        //private static void ScalarEncoderTest(int[] inputs, int a, int b)
        //{
        //    var outFolder1 = @"NEWTestFiles\NEWScalarEncoderResults";
        //    var outFolder2 = @"Overlap_Union";

        //    Directory.CreateDirectory(outFolder1);
        //    Directory.CreateDirectory(outFolder2);
        //    ScalarEncoder encoder = new ScalarEncoder(new Dictionary<string, object>()
        //    {
        //        { "W", 3},       // 2% Approx 
        //        { "N", 100},
        //        { "MinVal", (double)0},
        //        { "MaxVal", (double)99},
        //        { "Periodic", true},
        //        { "Name", "Scalar Sequence"},
        //        { "ClipInput", true},
        //    });
        //    Dictionary<double, int[]> sdrs = new Dictionary<double, int[]>();


        //    foreach (double input in inputs)
        //    {
        //        int[] result = encoder.Encode(input);

        //        Console.WriteLine($"Input = {input}");
        //        Console.WriteLine($"SDRs Generated = {NeoCortexApi.Helpers.StringifyVector(result)}");
        //        Console.WriteLine($"SDR As Text = {NeoCortexApi.Helpers.StringifyVector(ArrayUtils.IndexWhere(result, k => k == 1))}");


        //        int[,] twoDimenArray = ArrayUtils.Make2DArray<int>(result, (int)Math.Sqrt(result.Length), (int)Math.Sqrt(result.Length));
        //        int[,] twoDimArray = ArrayUtils.Transpose(twoDimenArray);
        //        NeoCortexUtils.DrawBitmap(twoDimArray, 1024, 1024, $"{outFolder1}\\{input}.png", Color.PaleGreen, Color.Blue, text: input.ToString());

        //        sdrs.Add(input, result);


        //    }


        //    // <summary>
        //    /// Calculate all required results.
        //    /// 1. Overlap and Union of the Binary arrays of two scalar values.
        //    ///    It cross compares the binary arrays  of any of the two scalar values User enters.
        //    /// 2. Creates bitmaps of the overlaping and non-overlaping regions of the two binary arrays entered by the User.
        //    /// 3. Creates bitmaps of interestion of Overlap and Union of two values.
        //    /// </summary>

        //    //Console.WriteLine("Encoder Binary array Created");
        //    // Console.WriteLine("Enter the two elements you want to Compare");
        //    // String a = Console.ReadLine();
        //    //String b = Console.ReadLine();


        //    // SimilarityResult(Convert.ToInt32(a), Convert.ToInt32(b), sdrs, outFolder1);
        //    SimilarityResult(a, b, sdrs, outFolder1);

    }

        //private static void SimilarityResult(int arr1, int arr2, Dictionary<double, int[]> sdrs, String folder)              // Function to check similarity between Inputs 
        //{


        //    List<int[,]> arrayOvr = new List<int[,]>();

        //    int h = arr1;
        //    int w = arr2;

        //    Console.WriteLine("SDR[h] = ");

        //    Console.WriteLine(Helpers.StringifyVector(sdrs[h]));

        //    Console.WriteLine("SDR[w] = ");



        //    Console.WriteLine(Helpers.StringifyVector(sdrs[w]));

        //    var Overlaparray = SdrRepresentation.OverlapArraFun(sdrs[h], sdrs[w]);
        //    Console.WriteLine("SDR of Overlap = ");
        //    Console.WriteLine(Helpers.StringifyVector(Overlaparray));
        //    int[,] twoDimenArray2 = ArrayUtils.Make2DArray<int>(Overlaparray, (int)Math.Sqrt(Overlaparray.Length), (int)Math.Sqrt(Overlaparray.Length));
        //    int[,] twoDimArray1 = ArrayUtils.Transpose(twoDimenArray2);
        //    NeoCortexUtils.DrawBitmap(twoDimArray1, 1024, 1024, $"{folder}\\Overlap_{h}_{w}.png", Color.PaleGreen, Color.Red, text: $"Overlap_{h}_{w}.png");

        //    //var unionArr = sdrs[h].Union(sdrs[w]).ToArray();                              //This function was not working. so, new Union function is created.
        //    var unionArr = Union(sdrs[h], sdrs[w]).ToArray();
        //    Console.WriteLine("SDR of Union = ");
        //    Console.WriteLine(Helpers.StringifyVector(unionArr));
        //    int[,] twoDimenArray4 = ArrayUtils.Make2DArray<int>(unionArr, (int)Math.Sqrt(unionArr.Length), (int)Math.Sqrt(unionArr.Length));
        //    int[,] twoDimArray3 = ArrayUtils.Transpose(twoDimenArray4);

        //    NeoCortexUtils.DrawBitmap(twoDimArray3, 1024, 1024, $"{folder}\\Union_{h}_{w}.png", Color.PaleGreen, Color.Green, text: $"Union_{h}_{w}.png");
        //    SdrRepresentation.DrawIntersections(twoDimArray3, twoDimArray1, 100, $"{folder}\\Intersection of Union and Overlap of {h}_{w}.png", Color.Black, Color.Gray, text: $"Intersection.png");

        //}

        //public static int[] Union(int[] arr1, int[] arr2)                        // To find union of of the Binary arrays of two scalar values.
        //{
            

        //    int[] union = new int[arr1.Length];

        //    for (int i = 0; i < arr1.Length; i++)
        //    {

        //        if (arr1[i] == 0 && arr2[i] == 0)
        //        {
        //            union[i] = 0;
        //        }
        //        else
        //        {
        //            union[i] = 1;
        //        }
        //    }
        //    return union;
        //}

   // }


}
