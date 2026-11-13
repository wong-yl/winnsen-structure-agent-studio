using System;
using System.IO;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class UpdateR59Drawings
{
    static void Main(string[] args)
    {
        var drawingFiles = new List<string>
        {
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\01_frame_门框 横隔板\门框 横隔板_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\02_frame_门框 横隔板R\门框 横隔板R_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\03_frame_门框 竖隔板L\门框 竖隔板L_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\04_frame_门框 竖隔板R\门框 竖隔板R_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\05_frame_门框 上\门框 上_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\06_frame_门框 下\门框 下_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\07_frame_门框 左\门框 左_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\08_frame_门框 右\门框 右_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\09_door_stiffener_H130\door_stiffener_W537_H130_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\10_door_stiffener_H179\door_stiffener_W537_H179_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\11_door_stiffener_H316\door_stiffener_W537_H316_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\12_reinforcement_箱体竖隔板加强件\箱体竖隔板加强件_R59.SLDDRW",
            @"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\13_base_底座底板\底座底板_R59.SLDDRW"
        };

        string outputDir = @"C:\Users\Administrator\Desktop\delivery_r59_parts_RELEASED";
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 连接 SolidWorks...");
        SldWorks swApp = (SldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));

        if (swApp == null)
        {
            Console.WriteLine("错误：无法连接到 SolidWorks");
            return;
        }

        swApp.Visible = true;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SolidWorks 已连接");

        int successCount = 0;
        int failCount = 0;

        for (int i = 0; i < drawingFiles.Count; i++)
        {
            string dwgFile = drawingFiles[i];
            Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] === 处理 {i+1}/13: {Path.GetFileName(dwgFile)} ===");

            if (!File.Exists(dwgFile))
            {
                Console.WriteLine($"  ❌ 文件不存在");
                failCount++;
                continue;
            }

            try
            {
                // 打开图纸
                Console.WriteLine($"  打开图纸...");
                int errors = 0, warnings = 0;
                ModelDoc2 doc = swApp.OpenDoc6(dwgFile, (int)swDocumentTypes_e.swDocDRAWING,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);

                if (doc == null || errors != 0)
                {
                    Console.WriteLine($"  ❌ 打开失败 (errors={errors}, warnings={warnings})");
                    failCount++;
                    continue;
                }

                DrawingDoc drawing = (DrawingDoc)doc;
                Console.WriteLine($"  ✓ 图纸已打开");

                // 更新自定义属性
                Console.WriteLine($"  更新属性...");
                CustomPropertyManager prp = doc.Extension.get_CustomPropertyManager("");

                // 设计、绘图、核对、审核、批准都设为 Eli
                prp.Set2("设计", "Eli");
                prp.Set2("绘图", "Eli");
                prp.Set2("核对", "Eli");
                prp.Set2("审核", "Eli");
                prp.Set2("批准", "Eli");

                // 日期设为 2026-11-02
                prp.Set2("日期", "2026-11-02");

                // 版本改为 R5
                prp.Set2("版本", "R5");

                Console.WriteLine($"  ✓ 属性已更新");

                // 保存
                Console.WriteLine($"  保存...");
                bool saveResult = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);

                if (!saveResult || errors != 0)
                {
                    Console.WriteLine($"  ❌ 保存失败 (errors={errors})");
                    swApp.CloseDoc(doc.GetTitle());
                    failCount++;
                    continue;
                }
                Console.WriteLine($"  ✓ 已保存");

                // 导出 PDF
                string pdfName = Path.GetFileNameWithoutExtension(dwgFile).Replace("_R59", "_R5_RELEASED") + ".pdf";
                string pdfPath = Path.Combine(outputDir, pdfName);

                Console.WriteLine($"  导出 PDF: {pdfName}");
                doc.Extension.SaveAs(pdfPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);

                if (errors != 0)
                {
                    Console.WriteLine($"  ❌ PDF 导出失败 (errors={errors})");
                }
                else
                {
                    Console.WriteLine($"  ✓ PDF 已导出");
                }

                // 关闭
                swApp.CloseDoc(doc.GetTitle());
                successCount++;
                Console.WriteLine($"  ✓ 完成 ({successCount}/13)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 异常: {ex.Message}");
                failCount++;
            }
        }

        Console.WriteLine($"\n\n========================================");
        Console.WriteLine($"批量处理完成");
        Console.WriteLine($"  成功: {successCount}");
        Console.WriteLine($"  失败: {failCount}");
        Console.WriteLine($"  总计: {drawingFiles.Count}");
        Console.WriteLine($"\n输出目录: {outputDir}");
        Console.WriteLine($"========================================");
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
}
