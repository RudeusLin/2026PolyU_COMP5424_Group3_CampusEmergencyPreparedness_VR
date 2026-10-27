// Generate stable transparent lettering without a runtime dynamic-font atlas.
// Run from the project root: swift -module-cache-path /tmp/campus-letter-cache tools/generate_corridor_letters.swift
import Foundation
import CoreGraphics
import CoreText
import ImageIO

let destination = URL(fileURLWithPath: "Assets/Environment/CampusSceneModifications", isDirectory: true)
try FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true)
let font = CTFontCreateWithName("ArialMT" as CFString, 900, nil)
for letter in ["A", "B"] {
    var character = Array(letter.utf16)[0]
    var glyph: CGGlyph = 0
    guard CTFontGetGlyphsForCharacters(font, &character, &glyph, 1),
          let path = CTFontCreatePathForGlyph(font, glyph, nil) else {
        fatalError("Arial glyph unavailable: \(letter)")
    }
    let bounds = path.boundingBoxOfPath
    let padding: CGFloat = 12
    let width = Int(ceil(bounds.width + padding * 2))
    let height = Int(ceil(bounds.height + padding * 2))
    let context = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8,
                            bytesPerRow: width * 4, space: CGColorSpaceCreateDeviceRGB(),
                            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    context.setAllowsAntialiasing(true)
    context.translateBy(x: padding - bounds.minX, y: padding - bounds.minY)
    context.addPath(path)
    context.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
    context.fillPath()
    let image = context.makeImage()!
    let output = destination.appendingPathComponent("Corridor-\(letter).png")
    let writer = CGImageDestinationCreateWithURL(output as CFURL, "public.png" as CFString, 1, nil)!
    CGImageDestinationAddImage(writer, image, nil)
    guard CGImageDestinationFinalize(writer) else { fatalError("Cannot write \(output.path)") }
    print("\(letter): \(width) x \(height) -> \(output.path)")
}
