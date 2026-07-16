using System.Text;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public class PlxParserServiceTests
{
    [Fact]
    public async Task ParseAsync_MapsCurriculumStructureAndCoursework()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Документ AppVersion="50000" Тип="РАБОЧИЙ УЧЕБНЫЙ ПЛАН" ЧислоКурсов="4" СеместровНаКурсе="3" КодФормыОбучения="1">
              <diffgr:diffgram xmlns:diffgr="urn:schemas-microsoft-com:xml-diffgram-v1">
                <dsMMISDB xmlns="http://tempuri.org/dsMMISDB.xsd">
                  <ООП Шифр="09.03.01" Название="Информатика и вычислительная техника" />
                  <Планы Титул="Бакалавриат 09.03.01" ГодНачалаПодготовки="2025" КодФормыОбучения="1" Квалификация="бакалавр" />
                  <ФормаОбучения Код="1" Название="Очная" />
                  <СправочникТипОбъекта Код="2" Название="Дисциплины" />
                  <СправочникТипОбъекта Код="3" Название="Практики (НИР)" />
                  <СправочникТипОбъекта Код="5" Название="Блоки по выбору" />
                  <СправочникТипОбъекта Код="6" Название="ГИА" />
                  <СправочникВидыРабот Код="5" Название="Курсовая работа" Аббревиатура="КР" />
                  <ПланыСтроки Код="-1" Дисциплина="Базы данных" ДисциплинаКод="Б1.О.01" ТипОбъекта="2" СчитатьВПлане="true" ЗЕТфакт="4" ЧасовПоПлану="144" />
                  <ПланыСтроки Код="-2" Дисциплина="Дисциплины по выбору" ДисциплинаКод="Б1.В.ДВ.01" ТипОбъекта="5" СчитатьВПлане="true" />
                  <ПланыСтроки Код="-3" Дисциплина="Учебная практика" ДисциплинаКод="Б2.О.01(У)" ТипОбъекта="3" СчитатьВПлане="true" />
                  <ПланыСтроки Код="-4" Дисциплина="Подготовка и защита ВКР" ДисциплинаКод="Б3.01" ТипОбъекта="6" СчитатьВПлане="true" />
                  <ПланыСтроки Код="-5" Дисциплина="Устаревшая дисциплина" ДисциплинаКод="Б1.О.99" ТипОбъекта="2" СчитатьВПлане="false" />
                  <ПланыНовыеЧасы КодОбъекта="-1" КодВидаРаботы="5" Курс="3" Семестр="2" Количество="1" />
                </dsMMISDB>
              </diffgr:diffgram>
            </Документ>
            """;

        var result = await ParseAsync(xml);

        Assert.Equal("09.03.01", result.PlanCode);
        Assert.Equal("Бакалавриат", result.EducationalLevel);
        Assert.Equal("Очная", result.EducationForm);
        Assert.Equal(2025, result.AdmissionYear);
        Assert.Equal(4, result.CoursesCount);
        Assert.Equal(6, result.Elements.Count(element => element.TypeElement == EducationalProgramElementTypes.Main));
        Assert.Single(result.Elements, element => element.TypeElement == EducationalProgramElementTypes.Discipline && element.Name == "Базы данных");
        Assert.Single(result.Elements, element => element.TypeElement == EducationalProgramElementTypes.Module);
        Assert.Single(result.Elements, element => element.TypeElement == EducationalProgramElementTypes.Practice);
        Assert.Single(result.Elements, element => element.TypeElement == EducationalProgramElementTypes.Gia);
        var coursework = Assert.Single(result.Elements, element => element.TypeElement == EducationalProgramElementTypes.Coursework);
        Assert.Equal("Б1.О.01; 8 семестр", coursework.Code);
        Assert.Equal(1, result.ExcludedRowsCount);
    }

    [Fact]
    public async Task ParseAsync_RejectsDocumentTypeDefinition()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE Документ [<!ENTITY x "test">]>
            <Документ>&x;</Документ>
            """;

        await Assert.ThrowsAsync<InvalidDataException>(() => ParseAsync(xml));
    }

    [Fact]
    public async Task ParseAsync_RejectsNonPlxRoot()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ParseAsync("<root />"));
    }

    private static async Task<PlxImportPreview> ParseAsync(string xml)
    {
        var parser = new PlxParserService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return await parser.ParseAsync(stream);
    }
}
