import Foundation
import CoreGraphics
import ImageIO

// Splits the existing four-view reference boards without resizing or redrawing.
// Rectangle boundaries exclude the thin panel separator when one is present.
struct Layout {
    let width: Int
    let height: Int
    let leftEnd: Int
    let rightStart: Int
    let topEnd: Int
    let bottomStart: Int
}

let layouts: [String: Layout] = [
    "a40": Layout(width: 1536, height: 1024, leftEnd: 560, rightStart: 560, topEnd: 500, bottomStart: 500),
    "h8": Layout(width: 1536, height: 1024, leftEnd: 679, rightStart: 684, topEnd: 426, bottomStart: 430),
    "j20": Layout(width: 1536, height: 1024, leftEnd: 765, rightStart: 769, topEnd: 407, bottomStart: 412),
    "m2": Layout(width: 1426, height: 1103, leftEnd: 523, rightStart: 527, topEnd: 548, bottomStart: 554),
    "r3": Layout(width: 1536, height: 1024, leftEnd: 650, rightStart: 655, topEnd: 434, bottomStart: 440),
    "r4": Layout(width: 1536, height: 1024, leftEnd: 765, rightStart: 771, topEnd: 506, bottomStart: 512),
    "t90": Layout(width: 1536, height: 1024, leftEnd: 509, rightStart: 515, topEnd: 510, bottomStart: 515),
    "u8": Layout(width: 1536, height: 1024, leftEnd: 575, rightStart: 581, topEnd: 491, bottomStart: 497),
    "v12": Layout(width: 1536, height: 1024, leftEnd: 607, rightStart: 612, topEnd: 507, bottomStart: 512),
]

let fileManager = FileManager.default
let root = URL(fileURLWithPath: CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : fileManager.currentDirectoryPath)
let output = root.appendingPathComponent("single-views", isDirectory: true)
try fileManager.createDirectory(at: output, withIntermediateDirectories: true)

for kind in layouts.keys.sorted() {
    guard let layout = layouts[kind] else { continue }
    for team in ["blue", "red"] {
        let sourceURL = root.appendingPathComponent("\(team)-\(kind).png")
        guard let source = CGImageSourceCreateWithURL(sourceURL as CFURL, nil),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
            fatalError("Cannot read \(sourceURL.path)")
        }
        guard image.width == layout.width && image.height == layout.height else {
            fatalError("Unexpected dimensions for \(sourceURL.lastPathComponent): \(image.width)x\(image.height)")
        }

        let rects: [(String, CGRect)] = [
            ("front", CGRect(x: 0, y: 0, width: layout.leftEnd, height: layout.topEnd)),
            ("left", CGRect(x: layout.rightStart, y: 0, width: layout.width - layout.rightStart, height: layout.topEnd)),
            ("back", CGRect(x: 0, y: layout.bottomStart, width: layout.leftEnd, height: layout.height - layout.bottomStart)),
            ("top", CGRect(x: layout.rightStart, y: layout.bottomStart, width: layout.width - layout.rightStart, height: layout.height - layout.bottomStart)),
        ]
        for (view, rect) in rects {
            guard let cropped = image.cropping(to: rect) else {
                fatalError("Crop failed: \(sourceURL.lastPathComponent) \(view)")
            }
            let destinationURL = output.appendingPathComponent("\(team)-\(kind)-\(view).png")
            guard let destination = CGImageDestinationCreateWithURL(destinationURL as CFURL, "public.png" as CFString, 1, nil) else {
                fatalError("Cannot create \(destinationURL.path)")
            }
            CGImageDestinationAddImage(destination, cropped, nil)
            guard CGImageDestinationFinalize(destination) else {
                fatalError("Cannot save \(destinationURL.path)")
            }
        }
    }
}

print("Created \(layouts.count * 2 * 4) view images in \(output.path)")
