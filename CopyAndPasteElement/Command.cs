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

//���\�t�g�ɂ�EPPlus4.5.3.3���g�p���Ă��܂��B�����LGPL���C�Z���X�ł��B���쌠��EPPlus Software�Ђł��B

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

            //�R�s�[�������I��--------------------------------------
            Reference sel = uidoc.Selection.PickObject(ObjectType.Element,"�R�s�[�������I��ł�������");
            if (sel == null)
            {
                TaskDialog.Show("�G���[", "�R�s�[����v�f���������I�΂�Ă��܂���");
                return Result.Cancelled;
            }
            Element e = doc.GetElement(sel);
            ElementId elementId = e.Id;

            //���݂̃G�������g�̈ʒu
            LocationPoint Lp = e.Location as LocationPoint;
            XYZ ElementPoint = Lp.Point as XYZ;

            //���T�C�Y
            double equipmentSizeX = Math.Abs(e.get_BoundingBox(doc.ActiveView).Max.X -
                e.get_BoundingBox(doc.ActiveView).Min.X);
            double equipmentSizeY = Math.Abs(e.get_BoundingBox(doc.ActiveView).Max.Y -
                e.get_BoundingBox(doc.ActiveView).Min.Y);

            //�z�u���@�ɂ���Form�Ń��[�U�[����---�c�Ɖ��̑䐔�𕷂�------------------
            //�������̑䐔�A������
            int wNumber = 0;
            //�c�����̑䐔�A������
            int dNumber = 0;

            using (Form1 thisForm = new Form1())
            {
                if (thisForm.ShowDialog() == DialogResult.OK)
                {
                    //OK�{�^���������ꂽ����͓��e����荞��
                    //�������i�������j�̑䐔
                    wNumber = thisForm.getWNumber();
                    //�c�����i���������j�̑䐔
                    dNumber = thisForm.getDNumber();
                    //�����̓K���`�F�b�N
                    if(wNumber < 1)
                    {
                        TaskDialog.Show("�G���[", "�������̑䐔��1�ȏ�łȂ���΂Ȃ�܂���");
                        return Result.Cancelled;
                    }
                    if (dNumber < 1)
                    {
                        TaskDialog.Show("�G���[", "�c�����̑䐔��1�ȏ�łȂ���΂Ȃ�܂���");
                        return Result.Cancelled;
                    }
                }
                else
                {
                    return Result.Cancelled;
                }
            }

            //�z�u����X�y�[�X��I��
            Space space = KUtil2.GetSpace(doc, uidoc);
            if (space == null)
            {
                TaskDialog.Show("�G���[", "�X�y�[�X���������I�΂�Ă��܂���");
                return Result.Cancelled;
            }

            //�X�y�[�X�̌��_�i�����̍ŏ��_�j���擾����
            XYZ origin = KUtil2.SpaceMin(doc, space);

            //�X�y�[�X�̕�width�Ɖ��sdepth
            string[] wd = KUtil2.SpaceWidthAndDepth(doc, space);
            double width = Convert.ToDouble(wd[0]);
            double depth = Convert.ToDouble(wd[1]);

            //�X�y�[�X���Ɏw��䐔�̊��[�܂邩�m�F����
            if(width < equipmentSizeX * wNumber)
            {
                TaskDialog.Show("�G���[", "�X�y�[�X�̉������Ɏw��䐔�̊��[�܂�܂���B�䐔�����炵�Ă�������");
                return Result.Cancelled;
            }
            if (depth < equipmentSizeY * dNumber)
            {
                TaskDialog.Show("�G���[", "�X�y�[�X�̏c�����Ɏw��䐔�̊��[�܂�܂���B�䐔�����炵�Ă�������");
                return Result.Cancelled;
            }

            //�������̊Ԋu���v�Z����
            double wDistance = width / wNumber;
            //�c�����̊Ԋu���v�Z����
            double dDistance = depth / dNumber;

            //�����̍ŏ��v�f�̈ʒu�����肷��B
            double lowerLevelOfSpace = space.Level.Elevation;//�ʕt�ł͂Ȃ����������Ƃ��A���̍�������GL�ʁi��ʁj����̍����ɂȂ�̂ŃX�y�[�X�̉������x��������
            XYZ newPosition = origin.Add(new XYZ(wDistance/2, dDistance/2, ElementPoint.Z - lowerLevelOfSpace));//���������͌��݂̊����̗p

            //�ŏ��̊����ړ������邽�߂̈ړ��x�N�g�����v�Z
            XYZ moveVector = newPosition - ElementPoint;

            //�R�s�[���Ĕz�u����
            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Copy Elements in Space");

                //�R�s�[�����1���ړ��x�N�g���������ړ�
                ElementTransformUtils.MoveElement(doc, elementId, moveVector);

                //�܂��������̃R�s�[�B���Ƃł���1����c�����ɃR�s�[����
                //���̑䐔�̂ق��������P�[�X���������̂ł͂Ȃ����B���������ɒ����i�����j
                IList<ElementId> elementsToCopy = new List<ElementId>();//�����̊���ID����
                elementsToCopy.Add(elementId);  // �ŏ��̊���ǉ�

                //�������̃R�s�[
                for (int i = 1; i < wNumber; i++)  // i=1����J�n���AwNumber�����܂�
                {
                    XYZ copyVector = new XYZ(i * wDistance, 0, 0);
                    ICollection<ElementId> copiedElementIds = ElementTransformUtils.CopyElement(doc, elementId, copyVector);
                    // �R�s�[���ꂽ����ElementId�����X�g�ɒǉ�
                    foreach (ElementId copiedElementId in copiedElementIds)
                    {
                        elementsToCopy.Add(copiedElementId);
                    }
                }
                //�c�����̃R�s�[
                for (int j = 1; j < dNumber; j++)
                {
                    //�R�s�[������R�s�[��܂ł̈ړ������x�N�g���B�����ł�Y�����̂�
                    XYZ copyVector = new XYZ(0, j * dDistance, 0);
                    
                    ElementTransformUtils.CopyElements(doc, elementsToCopy, copyVector);
                }

                tx.Commit();
                
            }
            return Result.Succeeded;
        }
    }
}
