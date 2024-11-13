using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CopyAndPasteElement
{
    public static class KUtil2
    {
        //Excelファイルを読むためのダイアログを表示する
        public static string OpenExcel()
        {
            string fileName = string.Empty;

            //OpenFileDialog
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "ファイル選択";
                openFileDialog.Filter = "ExcelFiles | *.xls;*.xlsx;*.xlsm";
                //初期表示フォルダはデスクトップ
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                //ファイル選択ダイアログを開く
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    fileName = openFileDialog.FileName;
                }
            }
            return fileName;
        }
        //ミリメーターを内部単位に変換する
        public static double CVmmToInt(double x)
        {
            return UnitUtils.ConvertToInternalUnits(x, UnitTypeId.Millimeters);
        }
        //内部単位をミリメーターに変換する
        public static double CVintTomm(double x)
        {
            return UnitUtils.ConvertFromInternalUnits(x, UnitTypeId.Millimeters);
        }
        //目的のレベル（階）を探して返す
        public static Level GetLevel(Document doc, string levelName)
        {
            Level result = null;
            //エレメントコレクターの作成
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            //レベルを全て検出する
            ICollection<Element> collection = collector.OfClass(typeof(Level)).ToElements();
            //目的のレベルを探す
            foreach (Element element in collection)
            {
                Level level = element as Level;
                if (null != level)
                {
                    if (level.Name == levelName)
                    {
                        result = level;
                    }
                }
            }
            return result;
        }
        //床のためのカーブを作る
        public static void CreateGrids(Document doc)
        {
            double y1 = -100;
            double y2 = 30;
            double[] x = { 2, 34, 74, 114, 147 };
            string[] symbX = { "1", "2", "3", "4", "5" };

            double x1 = -30;
            double x2 = 180;
            double[] y = { -2, -33, -66 };
            string[] symbY = { "A", "B", "C" };

            for (int i = 0; i < x.Length; i++)
            {
                XYZ start = new XYZ(x[i], y1, 0);
                XYZ end = new XYZ(x[i], y2, 0);
                Line line = Line.CreateBound(start, end);
                Grid grid = Grid.Create(doc, line);
                grid.Name = symbX[i];
            }

            for (int i = 0; i < y.Length; i++)
            {
                XYZ start = new XYZ(x1, y[i], 0);
                XYZ end = new XYZ(x2, y[i], 0);
                Line line = Line.CreateBound(start, end);
                Grid grid = Grid.Create(doc, line);
                grid.Name = symbY[i];
            }
        }
        //ユーザーがピックアップすることでモデルからスペースを取得する（現在モデルとリンクモデルに対応）
        public static Space GetSpaceCurrentAndLink(Document doc, UIDocument uidoc)
        {
            Reference spaceRef;//スペースのリファレンス
            ElementInLinkSelectionFilter<Space> filter = new ElementInLinkSelectionFilter<Space>(doc);
            try
            {
                spaceRef = uidoc.Selection.PickObject(ObjectType.PointOnElement, filter, "配置先のスペースを選んでください（このモデルでもリンクモデルでも）");
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            Space space;
            if (filter.LastCheckedWasFromLink)
            {
                space = filter.LinkedDocument.GetElement(spaceRef.LinkedElementId) as Space;
            }
            else
            {
                space = doc.GetElement(spaceRef) as Space;
            }
            return space;
        }
        public class ElementInLinkSelectionFilter<T> : ISelectionFilter where T : Element
        {
            private Document _doc;
            public ElementInLinkSelectionFilter(Document doc)
            {
                _doc = doc;
            }
            public Document LinkedDocument { get; private set; } = null;
            public bool LastCheckedWasFromLink
            {
                get { return null != LinkedDocument; }
            }
            public bool AllowElement(Element e)
            {
                return true;
            }
            public bool AllowReference(Reference r, XYZ p)
            {
                LinkedDocument = null;
                Element e = _doc.GetElement(r);
                if (e is RevitLinkInstance)
                {
                    RevitLinkInstance li = e as RevitLinkInstance;
                    LinkedDocument = li.GetLinkDocument();
                    e = LinkedDocument.GetElement(r.LinkedElementId);
                }
                return e is T;
            }
        }
        //ユーザーがピックアップすることでモデルからスペースを取得する（現在モデルのみ対応）
        public static Space GetSpace(Document doc, UIDocument uidoc)
        {
            try
            {
                Reference spaceRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new SpaceSelectionFilter(),
                    "配置先のスペースを選んでください");

                return doc.GetElement(spaceRef) as Space;
            }
            catch (OperationCanceledException)
            {
                //ユーザーがキャンセルした場合
                return null;
            }
        }

        public class SpaceSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Space;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
        //スペースのバウンディングボックスを返す
        public static XYZ SpaceMin(Document doc, Space space)
        {
            Autodesk.Revit.DB.View activeView = doc.ActiveView;
            BoundingBoxXYZ box = space.get_BoundingBox(activeView);
            XYZ min = box.Min;
            return min;
        }
        public static XYZ SpaceMax(Document doc, Space space)
        {
            Autodesk.Revit.DB.View activeView = doc.ActiveView;
            BoundingBoxXYZ box = space.get_BoundingBox(activeView);
            XYZ max = box.Max;
            return max;
        }

        public static string[] SpaceWidthAndDepth(Document doc, Space space)
        {
            string[] result = new string[2];// width, height

            Autodesk.Revit.DB.View activeView = doc.ActiveView;
            BoundingBoxXYZ box = space.get_BoundingBox(activeView);
            XYZ min = box.Min;
            XYZ max = box.Max;
            double width = Math.Abs(max.X - min.X);
            double depth = Math.Abs(max.Y - min.Y);

            result[0] = width.ToString();
            result[1] = depth.ToString();

            return result;
        }
    }
}
