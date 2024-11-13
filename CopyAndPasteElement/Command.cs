#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

#endregion

//当ソフトにはEPPlus4.5.3.3を使用しています。これはLGPLライセンスです。著作権はEPPlus Software社です。

namespace CopyAndPasteElement
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;
            Document doc = uidoc.Document;

            //コピーする器具を選ぶ--------------------------------------
            Reference sel = uidoc.Selection.PickObject(ObjectType.Element,"コピーする器具を選んでください");
            if (sel == null)
            {
                TaskDialog.Show("エラー", "コピーする要素が正しく選ばれていません");
                return Result.Cancelled;
            }
            Element e = doc.GetElement(sel);
            ElementId elementId = e.Id;

            //現在のエレメントの位置
            LocationPoint Lp = e.Location as LocationPoint;
            XYZ ElementPoint = Lp.Point as XYZ;

            //器具サイズ
            double equipmentSizeX = Math.Abs(e.get_BoundingBox(doc.ActiveView).Max.X -
                e.get_BoundingBox(doc.ActiveView).Min.X);
            double equipmentSizeY = Math.Abs(e.get_BoundingBox(doc.ActiveView).Max.Y -
                e.get_BoundingBox(doc.ActiveView).Min.Y);

            //配置方法についてFormでユーザー入力---縦と横の台数を聞く------------------
            //横方向の台数、初期化
            int wNumber = 0;
            //縦方向の台数、初期化
            int dNumber = 0;

            using (Form1 thisForm = new Form1())
            {
                if (thisForm.ShowDialog() == DialogResult.OK)
                {
                    //OKボタンが押されたら入力内容を取り込む
                    //横方向（幅方向）の台数
                    wNumber = thisForm.getWNumber();
                    //縦方向（高さ方向）の台数
                    dNumber = thisForm.getDNumber();
                    //数字の適正チェック
                    if(wNumber < 1)
                    {
                        TaskDialog.Show("エラー", "横方向の台数は1以上でなければなりません");
                        return Result.Cancelled;
                    }
                    if (dNumber < 1)
                    {
                        TaskDialog.Show("エラー", "縦方向の台数は1以上でなければなりません");
                        return Result.Cancelled;
                    }
                }
                else
                {
                    return Result.Cancelled;
                }
            }

            //配置するスペースを選ぶ
            Space space = KUtil2.GetSpace(doc, uidoc);
            if (space == null)
            {
                TaskDialog.Show("エラー", "スペースが正しく選ばれていません");
                return Result.Cancelled;
            }

            //スペースの原点（左下の最小点）を取得する
            XYZ origin = KUtil2.SpaceMin(doc, space);

            //スペースの幅widthと奥行depth
            string[] wd = KUtil2.SpaceWidthAndDepth(doc, space);
            double width = Convert.ToDouble(wd[0]);
            double depth = Convert.ToDouble(wd[1]);

            //スペース内に指定台数の器具が納まるか確認する
            if(width < equipmentSizeX * wNumber)
            {
                TaskDialog.Show("エラー", "スペースの横方向に指定台数の器具が納まりません。台数を減らしてください");
                return Result.Cancelled;
            }
            if (depth < equipmentSizeY * dNumber)
            {
                TaskDialog.Show("エラー", "スペースの縦方向に指定台数の器具が納まりません。台数を減らしてください");
                return Result.Cancelled;
            }

            //横方向の間隔を計算する
            double wDistance = width / wNumber;
            //縦方向の間隔を計算する
            double dDistance = depth / dNumber;

            //左下の最初要素の位置を決定する。
            double lowerLevelOfSpace = space.Level.Elevation;//面付ではない器具を扱うとき、器具の高さ情報はGL面（基準面）からの高さになるのでスペースの下限レベルを引く
            XYZ newPosition = origin.Add(new XYZ(wDistance/2, dDistance/2, ElementPoint.Z - lowerLevelOfSpace));//高さ方向は現在の器具高さ採用

            //最初の器具を移動させるための移動ベクトルを計算
            XYZ moveVector = newPosition - ElementPoint;

            //コピーして配置する
            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Copy Elements in Space");

                //コピー元器具1個を移動ベクトル分だけ移動
                ElementTransformUtils.MoveElement(doc, elementId, moveVector);

                //まず横方向のコピー。あとでこの1列を縦方向にコピーする
                //横の台数のほうが多いケースがおおいのではないか。部屋が横に長い（推測）
                IList<ElementId> elementsToCopy = new List<ElementId>();//横一列の器具のIDたち
                elementsToCopy.Add(elementId);  // 最初の器具を追加

                //横方向のコピー
                for (int i = 1; i < wNumber; i++)  // i=1から開始し、wNumber未満まで
                {
                    XYZ copyVector = new XYZ(i * wDistance, 0, 0);
                    ICollection<ElementId> copiedElementIds = ElementTransformUtils.CopyElement(doc, elementId, copyVector);
                    // コピーされた器具のElementIdをリストに追加
                    foreach (ElementId copiedElementId in copiedElementIds)
                    {
                        elementsToCopy.Add(copiedElementId);
                    }
                }
                //縦方向のコピー
                for (int j = 1; j < dNumber; j++)
                {
                    //コピー元からコピー先までの移動距離ベクトル。ここではY方向のみ
                    XYZ copyVector = new XYZ(0, j * dDistance, 0);
                    
                    ElementTransformUtils.CopyElements(doc, elementsToCopy, copyVector);
                }

                tx.Commit();
                
            }
            return Result.Succeeded;
        }
    }
}
