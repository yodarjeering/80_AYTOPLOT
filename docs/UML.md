


classDiagram
    direction LR

    class App {
        <<WPF Application>>
    }

    class MainView {
        <<Window>>
        -bool isDrawing
        -Polyline currentLine
        -Point _prevPoint
        +MainView()
        -ImagePathTextBox_Drop(sender, e)
        -DrawCanvas_MouseDown(sender, e)
        -DrawCanvas_MouseMove(sender, e)
        -DrawCanvas_MouseUp(sender, e)
        -OnShowPathInputDialog(sender, e)
        -Window_KeyDown(sender, e)
    }

    class MainViewModel {
        <<ObservableObject>>
        -ImageProcessingService _service
        -BitmapSource _inputBitmap
        -BitmapSource _originalBitmap
        -Rect _roi
        -Mat _plotArea
        -Mat _workingImage
        -Mat _originalPlotArea
        -Mat _noiseMask
        -AxisSettings _axisSettings
        -DisplayState _displayState
        +string ImagePath
        +string ResultText
        +CurveData CurveData
        +BitmapSource InputBitmap
        +BitmapSource GraphBitmap
        +IRelayCommand LoadImageCommand
        +IRelayCommand AxisCalibrationCommand
        +IRelayCommand ShowSeriesTraceWindowCommand
        +IRelayCommand ShowNoiseRemovalWindowCommand
        +IRelayCommand OnShowUpdateGraphCommand
        +IRelayCommand CopyCurveDataCommand
        +DrawNoiseMaskFromCanvas(prevCanvasPt, currCanvasPt)
        +LoadImageFromClipboard(bitmap)
        -OnLoadImage()
        -OnAxisCalibration()
        -ApplyAxisCalibration(vm)
        -OnShowNoiseRemovalWindow()
        -OnShowSeriesTraceWindow()
        -OnShowUpdateGraph()
        -DetectSeriesFromTraceGuides(rawTraceSeries)
        -BuildCurveDataText(data) string
    }

    class ImageProcessingService {
        -ImageProcessor _processor
        +RunRoi(inputImage) Rect
        +RunPlotArea(plotAreaForGrid, plotAreaForAnalysis, roi, workingImageSize, xMinInput, xMaxInput, yMinInput, yMaxInput, xScale, yScale) CurveData
        +CreateRoiHighlightImage(src, roi) Mat
        +ApplyNoiseMask(plotArea, noiseMask) Mat
    }

    class ImageProcessor {
        +DetectPlotRoi(workingImage) Rect
        +ProcessPlotArea(plotAreaForGrid, plotAreaForAnalysis, roi, workingImageSize, xMinInput, xMaxInput, yMinInput, yMaxInput, xScale, yScale) CurveData
        -CalculatePlotRoi(verticalLines, horizontalLines) Rect
        -ExtractLineSegments(lineImage, isHorizontal) List~LineSegmentPoint~
        -CreateGraphOverlay(imageSize, graphPoints, roiOffset) Mat
    }

    class OpenCvUtils {
        <<static>>
        +ReadImage(path) Mat
        +LoadBitmap(path) BitmapImage
        +BitmapImageToMat(bitmap) Mat
        +RenderGraphFromCurveData(data, width, height, xMin, xMax, yMin, yMax, isXLog, isYLog) Mat
        +RenderGraphFromSeries(seriesList, width, height, xMin, xMax, yMin, yMax, isXLog, isYLog) Mat
    }

    class PixelConverter {
        <<static>>
        +PxToReal(px, pxMin, pxMax, vMin, vMax, scale, invert) double[]
    }

    class AxisCalibrationDialog {
        <<Window>>
    }

    class AxisCalibrationDialogViewModel {
        <<ObservableObject>>
        +double XMin
        +double XMax
        +bool IsXLog
        +double YMin
        +double YMax
        +bool IsYLog
    }

    class NoiseRemovalWindow {
        <<Window>>
        -bool _isDrawing
        -Point _prevPoint
        +NoiseRemovalWindow()
        -PreviewImageControl_MouseLeftButtonDown(sender, e)
        -PreviewImageControl_MouseMove(sender, e)
        -PreviewImageControl_MouseLeftButtonUp(sender, e)
        -OnOkClicked(sender, e)
        -OnCancelClicked(sender, e)
    }

    class NoiseRemovalViewModel {
        <<ObservableObject>>
        -Stack~Mat~ _undoStack
        -Stack~Mat~ _redoStack
        -Mat _plotArea
        -Mat _noiseMask
        +BitmapSource PreviewImage
        +bool IsConfirmed
        +Mat ResultPlotArea
        +IRelayCommand UndoCommand
        +IRelayCommand RedoCommand
        +IRelayCommand ResetCommand
        +BeginStroke()
        +DrawMaskLine(p1, p2, canvasW, canvasH)
        +Confirm()
    }

    class SeriesTraceWindow {
        <<Window>>
        -bool _isDrawing
        -Polyline _currentLine
        +SeriesTraceWindow()
        -TraceCanvas_MouseLeftButtonDown(sender, e)
        -TraceCanvas_MouseMove(sender, e)
        -TraceCanvas_MouseLeftButtonUp(sender, e)
        -RedrawCompletedSeries()
    }

    class SeriesTraceViewModel {
        <<ObservableObject>>
        +int SeriesCount
        +int CurrentSeriesIndex
        +bool IsTracingActive
        +BitmapSource PlotImage
        +ObservableCollection TracedSeries
        +bool IsConfirmed
        +List ResultSeries
        +string InstructionText
        +bool CanTrace
        +BeginSeries(point)
        +AddPoint(point)
        +CompleteSeries()
        -StartTrace()
        -ResetTrace()
        -Undo()
        -Redo()
        -Ok()
    }

    class CurveDataCopyDialog {
        <<Window>>
    }

    class CurveDataCopyDialogViewModel {
        <<ObservableObject>>
        +string CurveText
        -CopyToClipboard()
    }

    class AxisSettings {
        +double XMin
        +double XMax
        +bool IsXLog
        +double YMin
        +double YMax
        +bool IsYLog
    }

    class CurveData {
        +List~ImagePoint~ Points
        +Rect PlotRoi
        +Mat OverlayGraphMat
    }

    class ImagePoint {
        <<struct>>
        +double X
        +double Y
    }

    class DisplayState {
        <<enumeration>>
        None
        Original
        AxisCalibrated
        NoiseRemoval
        GraphPlot
    }

    App --> MainView : StartupUri
    MainView --> MainViewModel : DataContext
    MainViewModel *-- ImageProcessingService
    ImageProcessingService *-- ImageProcessor
    MainViewModel o-- AxisSettings
    MainViewModel o-- CurveData
    MainViewModel --> DisplayState
    MainViewModel ..> OpenCvUtils
    MainViewModel ..> PixelConverter
    MainViewModel ..> AxisCalibrationDialog
    MainViewModel ..> AxisCalibrationDialogViewModel
    MainViewModel ..> NoiseRemovalWindow
    MainViewModel ..> NoiseRemovalViewModel
    MainViewModel ..> SeriesTraceWindow
    MainViewModel ..> SeriesTraceViewModel
    MainViewModel ..> CurveDataCopyDialog
    MainViewModel ..> CurveDataCopyDialogViewModel
    ImageProcessor ..> PixelConverter
    ImageProcessor --> CurveData
    CurveData *-- ImagePoint
    NoiseRemovalWindow --> NoiseRemovalViewModel : DataContext
    SeriesTraceWindow --> SeriesTraceViewModel : DataContext
    AxisCalibrationDialog --> AxisCalibrationDialogViewModel : DataContext
    CurveDataCopyDialog --> CurveDataCopyDialogViewModel : DataContext
```



sequenceDiagram
    actor User
    participant MainView
    participant MainVM as MainViewModel
    participant CvUtils as OpenCvUtils
    participant Service as ImageProcessingService
    participant Processor as ImageProcessor
    participant AxisDialog as AxisCalibrationDialog
    participant NoiseDialog as NoiseRemovalWindow
    participant TraceDialog as SeriesTraceWindow

    User->>MainView: Select, drop, or paste image
    MainView->>MainVM: Set ImagePath or LoadImageFromClipboard()
    MainVM->>CvUtils: LoadBitmap() / BitmapImageToMat()
    MainVM->>Service: RunRoi(inputImage)
    Service->>Processor: DetectPlotRoi(inputImageClone)
    Processor-->>Service: Rect roi
    Service-->>MainVM: Rect roi
    MainVM-->>MainView: InputBitmap = original image

    User->>MainView: Settings
    MainView->>MainVM: AxisCalibrationCommand
    MainVM->>Service: CreateRoiHighlightImage(src, roi)
    MainVM->>AxisDialog: ShowDialog(axis settings)
    AxisDialog-->>MainVM: X/Y min-max and scale
    MainVM->>Service: RunPlotArea(plotArea, null, roi, settings)
    Service->>Processor: ProcessPlotArea(...)
    Processor->>Processor: Remove grid and extract curve pixels
    Processor->>Processor: Convert pixels to real values
    Processor-->>Service: CurveData
    Service-->>MainVM: CurveData
    MainVM-->>MainView: InputBitmap = plot area

    opt Noise removal
        User->>MainView: Noise removal
        MainView->>MainVM: ShowNoiseRemovalWindowCommand
        MainVM->>NoiseDialog: ShowDialog(plotArea, originalPlotArea)
        User->>NoiseDialog: Draw mask and confirm
        NoiseDialog-->>MainVM: ResultPlotArea
        MainVM->>Service: RunPlotArea(beforeMask, afterMask, roi, settings)
        Service->>Processor: ProcessPlotArea(...)
        Processor-->>MainVM: CurveData
    end

    opt Manual series trace
        User->>MainView: Trace Series
        MainView->>MainVM: ShowSeriesTraceWindowCommand
        MainVM->>TraceDialog: ShowDialog(plotArea)
        User->>TraceDialog: Trace each series
        TraceDialog-->>MainVM: List of guide points
        MainVM->>MainVM: DetectSeriesFromTraceGuides()
        MainVM->>MainVM: ConvertPixelSeriesToRealPoints()
    end

    User->>MainView: Show graph
    MainView->>MainVM: OnShowUpdateGraphCommand
    MainVM->>CvUtils: RenderGraphFromCurveData() or RenderGraphFromSeries()
    CvUtils-->>MainVM: graph Mat
    MainVM-->>MainView: InputBitmap overlay and GraphBitmap

    User->>MainView: Copy data
    MainView->>MainVM: CopyCurveDataCommand
    MainVM->>MainVM: BuildCurveDataText()
```



stateDiagram-v2
    [*] --> None
    None --> Original: Load image or paste image
    Original --> AxisCalibrated: Apply axis calibration
    AxisCalibrated --> NoiseRemoval: Start inline mask drawing
    NoiseRemoval --> AxisCalibrated: Complete noise removal
    AxisCalibrated --> AxisCalibrated: NoiseRemovalWindow confirmed
    AxisCalibrated --> AxisCalibrated: SeriesTraceWindow confirmed
    AxisCalibrated --> GraphPlot: Show graph
    GraphPlot --> Original: Show original
    AxisCalibrated --> Original: Show original
    Original --> Original: Load another image
    GraphPlot --> AxisCalibrated: Re-run calibration or noise removal
```



flowchart LR
    subgraph WPF["WPF UI"]
        App["App.xaml"]
        MainView["MainView"]
        AxisDialog["AxisCalibrationDialog"]
        NoiseWindow["NoiseRemovalWindow"]
        SeriesWindow["SeriesTraceWindow"]
        CopyDialog["CurveDataCopyDialog"]
    end

    subgraph VM["ViewModels"]
        MainVM["MainViewModel"]
        AxisVM["AxisCalibrationDialogViewModel"]
        NoiseVM["NoiseRemovalViewModel"]
        SeriesVM["SeriesTraceViewModel"]
        CopyVM["CurveDataCopyDialogViewModel"]
    end

    subgraph Domain["Models"]
        AxisSettings["AxisSettings"]
        CurveData["CurveData"]
        ImagePoint["ImagePoint"]
        DisplayState["DisplayState"]
    end

    subgraph Processing["Image Processing"]
        Service["ImageProcessingService"]
        Processor["ImageProcessor"]
        PixelConverter["PixelConverter"]
        OpenCvUtils["OpenCvUtils"]
    end

    subgraph External["External Libraries"]
        Wpf["WPF"]
        Toolkit["CommunityToolkit.Mvvm"]
        OpenCv["OpenCvSharp"]
        MathNet["MathNet.Numerics"]
        Material["MaterialDesignThemes"]
    end

    App --> MainView
    MainView --> MainVM
    AxisDialog --> AxisVM
    NoiseWindow --> NoiseVM
    SeriesWindow --> SeriesVM
    CopyDialog --> CopyVM

    MainVM --> AxisSettings
    MainVM --> CurveData
    MainVM --> DisplayState
    CurveData --> ImagePoint

    MainVM --> Service
    Service --> Processor
    MainVM --> OpenCvUtils
    MainVM --> PixelConverter
    Processor --> PixelConverter

    MainView --> Wpf
    MainVM --> Toolkit
    AxisVM --> Toolkit
    NoiseVM --> Toolkit
    SeriesVM --> Toolkit
    CopyVM --> Toolkit
    Service --> OpenCv
    Processor --> OpenCv
    OpenCvUtils --> OpenCv
    PixelConverter --> Wpf
    Processor --> MathNet
    App --> Material
    MainView --> Material
```
