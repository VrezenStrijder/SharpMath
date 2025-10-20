using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageRecognition;

namespace SharpMath.Test
{
    public class ImageRecognitionCase
    {
        public static void UnifiedOCRCase()
        {
            string dataPath = @"D:\Dev\projects\self\SharpMath\tessdata";
            string modelsPath = @"D:\Dev\projects\self\SharpMath\Models\ocr_model";
            string latexModelsPath = @"D:\Dev\projects\self\SharpMath\Models\latex_model";
            string samplingPath = @"D:\Dev\projects\self\SharpMath\Samples";
            string mixedModelPath = @"D:\Dev\projects\self\SharpMath\Models\mixed_model";

            MixedOCREngine engine = new MixedOCREngine(mixedModelPath);
            var result = engine.ProcessImage(Path.Combine(samplingPath, "1.jpg"));


            //var ocrService = new UnifiedOCRService(dataPath, modelsPath,latexModelsPath);

            //var options6 = new OCRProcessingOptions()
            //{
            //    ConfidenceThreshold = 0.7f,
            //    LaTeXModelType = LatexModelType.Pix2Tex,
            //    PreferMixedContent = false,
            //    UseAdvancedSegmentation = false
            //};
            //var result6 = ocrService.ProcessImage(Path.Combine(samplingPath, "latex1.png"), options6);


            // 简单Latex模型，高级分割
            //var options1 = new OCRProcessingOptions()
            //{
            //    ConfidenceThreshold = 0.7f,
            //    LaTeXModelType =  LatexModelType.Simple,
            //    PreferMixedContent = false,
            //    UseAdvancedSegmentation = true
            //};
            //var result1 = ocrService.ProcessImage(Path.Combine(samplingPath, "1.jpg"), options1);


            // 简单Latex模型，混合识别
            //var options2 = new OCRProcessingOptions()
            //{
            //    ConfidenceThreshold = 0.7f,
            //    LaTeXModelType = LatexModelType.Simple,
            //    PreferMixedContent = true,
            //    UseAdvancedSegmentation = false
            //};
            //var result2 = ocrService.ProcessImage(Path.Combine(samplingPath, "1.jpg"), options2);


            // 简单Latex模型，标准处理
            //var options3 = new OCRProcessingOptions()
            //{
            //    ConfidenceThreshold = 0.7f,
            //    LaTeXModelType = LatexModelType.Simple,
            //    PreferMixedContent = false,
            //    UseAdvancedSegmentation = false
            //};
            //var result3 = ocrService.ProcessImage(Path.Combine(samplingPath, "1.jpg"), options3);


            // Pix2Tex模型，高级分割
            //var options4 = new OCRProcessingOptions()
            //{
            //    ConfidenceThreshold = 0.7f,
            //    LaTeXModelType = LatexModelType.Pix2Tex,
            //    PreferMixedContent = false,
            //    UseAdvancedSegmentation = true
            //};
            //var result4 = ocrService.ProcessImage(Path.Combine(samplingPath, "1.jpg"), options4);


            // Pix2Tex模型，混合识别
            //var options5 = new OCRProcessingOptions()
            //{
            //    ConfidenceThreshold = 0.7f,
            //    LaTeXModelType = LatexModelType.Pix2Tex,
            //    PreferMixedContent = true,
            //    UseAdvancedSegmentation = false
            //};
            //var result5 = ocrService.ProcessImage(Path.Combine(samplingPath, "1.jpg"), options5);


            // Pix2Tex模型，标准处理
            //var options6 = new OCRProcessingOptions()
            //{
            //    ConfidenceThreshold = 0.7f,
            //    LaTeXModelType = LatexModelType.Pix2Tex,
            //    PreferMixedContent = false,
            //    UseAdvancedSegmentation = false
            //};
            //var result6 = ocrService.ProcessImage(Path.Combine(samplingPath, "1.jpg"), options6);

        }
    }
}
