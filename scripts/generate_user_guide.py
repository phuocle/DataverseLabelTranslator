import os
import docx
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import parse_xml, OxmlElement
from docx.oxml.ns import nsdecls, qn

# Colors
COLOR_PRIMARY = RGBColor(31, 78, 121)     # #1F4E79 - Dark Steel Blue
COLOR_SECONDARY = RGBColor(47, 85, 151)   # #2F5597 - Classic Blue
COLOR_BODY = RGBColor(51, 51, 51)         # #333333 - Charcoal
COLOR_TEXT_MUTED = RGBColor(128, 128, 128)
HEX_PRIMARY = "1F4E79"
HEX_LIGHT_GRAY = "F2F4F7"
COLOR_CALLOUT_BG = "F2F4F7"
COLOR_SCREENSHOT_BG = "FCF3CF"
HEX_YELLOW_BG = "FCF3CF"

def set_cell_background(cell, hex_color):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = parse_xml(f'<w:shd {nsdecls("w")} w:fill="{hex_color}"/>')
    tcPr.append(shd)

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

def add_page_number(run):
    fldChar1 = parse_xml(r'<w:fldChar %s w:fldCharType="begin"/>' % nsdecls('w'))
    instrText = parse_xml(r'<w:instrText %s xml:space="preserve"> PAGE </w:instrText>' % nsdecls('w'))
    fldChar2 = parse_xml(r'<w:fldChar %s w:fldCharType="separate"/>' % nsdecls('w'))
    fldChar3 = parse_xml(r'<w:fldChar %s w:fldCharType="end"/>' % nsdecls('w'))
    run._r.append(fldChar1)
    run._r.append(instrText)
    run._r.append(fldChar2)
    run._r.append(fldChar3)

def add_total_pages(run):
    fldChar1 = parse_xml(r'<w:fldChar %s w:fldCharType="begin"/>' % nsdecls('w'))
    instrText = parse_xml(r'<w:instrText %s xml:space="preserve"> NUMPAGES </w:instrText>' % nsdecls('w'))
    fldChar2 = parse_xml(r'<w:fldChar %s w:fldCharType="separate"/>' % nsdecls('w'))
    fldChar3 = parse_xml(r'<w:fldChar %s w:fldCharType="end"/>' % nsdecls('w'))
    run._r.append(fldChar1)
    run._r.append(instrText)
    run._r.append(fldChar2)
    run._r.append(fldChar3)

def setup_header_footer(section, doc_title):
    section.different_first_page_header_footer = True
    
    # Header Setup
    header = section.header
    hp = header.paragraphs[0]
    hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    hrun = hp.add_run(doc_title)
    hrun.font.name = 'Segoe UI'
    hrun.font.size = Pt(8.5)
    hrun.font.color.rgb = COLOR_TEXT_MUTED
    
    # Footer Setup
    footer = section.footer
    fp = footer.paragraphs[0]
    fp.text = "" # Clear default
    fp.paragraph_format.tab_stops.add_tab_stop(Inches(6.5), 2) # Right align tab
    
    r_left = fp.add_run("Dataverse Label Translator  |  User Guide")
    r_left.font.name = 'Segoe UI'
    r_left.font.size = Pt(8.5)
    r_left.font.color.rgb = COLOR_TEXT_MUTED
    
    fp.add_run("\tPage ")
    r_page = fp.add_run()
    r_page.font.name = 'Segoe UI'
    r_page.font.size = Pt(8.5)
    r_page.font.color.rgb = COLOR_TEXT_MUTED
    add_page_number(r_page)
    
    fp.add_run(" of ")
    r_total = fp.add_run()
    r_total.font.name = 'Segoe UI'
    r_total.font.size = Pt(8.5)
    r_total.font.color.rgb = COLOR_TEXT_MUTED
    add_total_pages(r_total)

def add_callout(doc, text, title="CHÚ Ý", hex_bg=COLOR_CALLOUT_BG, is_screenshot=False):
    p = doc.add_paragraph()
    p.paragraph_format.left_indent = Inches(0.25)
    p.paragraph_format.right_indent = Inches(0.25)
    p.paragraph_format.space_before = Pt(12)
    p.paragraph_format.space_after = Pt(12)
    
    pPr = p._p.get_or_add_pPr()
    border_color = "FFC000" if is_screenshot else HEX_PRIMARY
    pbdr = parse_xml(f'<w:pBdr {nsdecls("w")}><w:left w:val="single" w:sz="24" w:space="8" w:color="{border_color}"/></w:pBdr>')
    pPr.append(pbdr)
    
    shd = parse_xml(f'<w:shd {nsdecls("w")} w:fill="{hex_bg}"/>')
    pPr.append(shd)
    
    run_title = p.add_run(f"[{title}] " if title else "")
    run_title.bold = True
    run_title.font.name = 'Segoe UI'
    run_title.font.size = Pt(10.5)
    if is_screenshot:
        run_title.font.color.rgb = RGBColor(180, 100, 0)
    else:
        run_title.font.color.rgb = COLOR_PRIMARY
        
    run_text = p.add_run(text)
    run_text.font.name = 'Segoe UI'
    run_text.font.size = Pt(10)
    run_text.italic = True
    run_text.font.color.rgb = COLOR_BODY
    return p

def add_heading_1(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(18)
    p.paragraph_format.space_after = Pt(6)
    p.paragraph_format.keep_with_next = True
    
    run = p.add_run(text)
    run.font.name = 'Segoe UI'
    run.font.size = Pt(16)
    run.bold = True
    run.font.color.rgb = COLOR_PRIMARY
    
    pPr = p._p.get_or_add_pPr()
    pbdr = parse_xml(f'<w:pBdr {nsdecls("w")}><w:bottom w:val="single" w:sz="6" w:space="4" w:color="{HEX_PRIMARY}"/></w:pBdr>')
    pPr.append(pbdr)
    return p

def add_heading_2(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(14)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.keep_with_next = True
    
    run = p.add_run(text)
    run.font.name = 'Segoe UI'
    run.font.size = Pt(13)
    run.bold = True
    run.font.color.rgb = COLOR_SECONDARY
    return p

def add_heading_3(doc, text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(10)
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.keep_with_next = True
    
    run = p.add_run(text)
    run.font.name = 'Segoe UI'
    run.font.size = Pt(11)
    run.bold = True
    run.font.color.rgb = COLOR_PRIMARY
    return p

def add_body_text(doc, text, bold_prefix=None, space_after=6, bullet=False):
    p = doc.add_paragraph(style='List Bullet' if bullet else 'Normal')
    p.paragraph_format.space_after = Pt(space_after)
    p.paragraph_format.line_spacing = 1.15
    
    if bold_prefix:
        r_prefix = p.add_run(bold_prefix)
        r_prefix.font.name = 'Segoe UI'
        r_prefix.font.size = Pt(10.5)
        r_prefix.bold = True
        r_prefix.font.color.rgb = COLOR_BODY
        
    run = p.add_run(text)
    run.font.name = 'Segoe UI'
    run.font.size = Pt(10.5)
    run.font.color.rgb = COLOR_BODY
    return p

def build_document():
    doc = Document()
    
    # Page setup
    for section in doc.sections:
        section.top_margin = Inches(1)
        section.bottom_margin = Inches(1)
        section.left_margin = Inches(1)
        section.right_margin = Inches(1)
        setup_header_footer(section, "Dataverse Label Translator - User Guide")
        
    # --- COVER PAGE ---
    p_spacer = doc.add_paragraph()
    p_spacer.paragraph_format.space_before = Pt(80)
    
    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r_title = p_title.add_run("DATAVERSE LABEL TRANSLATOR")
    r_title.font.name = 'Segoe UI'
    r_title.font.size = Pt(32)
    r_title.bold = True
    r_title.font.color.rgb = COLOR_PRIMARY
    
    p_subtitle = doc.add_paragraph()
    p_subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_subtitle.paragraph_format.space_after = Pt(140)
    r_sub = p_subtitle.add_run("User Guide & Deployment Documentation for Microsoft AppSource")
    r_sub.font.name = 'Segoe UI'
    r_sub.font.size = Pt(14)
    r_sub.font.color.rgb = COLOR_TEXT_MUTED
    
    p_info = doc.add_paragraph()
    p_info.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_info.paragraph_format.space_after = Pt(6)
    r_info = p_info.add_run("Publisher: Phuoc Le\nVersion: 1.0.0.0\nPlatform: Microsoft Power Platform / Dataverse")
    r_info.font.name = 'Segoe UI'
    r_info.font.size = Pt(11)
    r_info.font.color.rgb = COLOR_BODY
    
    doc.add_page_break()
    
    # --- TABLE OF CONTENTS ---
    add_heading_1(doc, "Table of Contents")
    add_body_text(doc, "[This document contains an automatic Table of Contents. Please right-click here and select 'Update Field' to generate the dynamic table after opening this document in Microsoft Word.]")
    p_toc_hint = doc.add_paragraph()
    p_toc_hint.paragraph_format.space_after = Pt(24)
    
    doc.add_page_break()
    
    # --- CONTENT ---
    add_heading_1(doc, "1. Introduction")
    add_body_text(doc, "The Dataverse Label Translator is a powerful model-driven application designed to streamline the translation and customization of user interface (UI) labels and metadata across Dynamics 365 and Microsoft Dataverse environments. Translating multiple languages in Dataverse can be a tedious process using native export/import translation functions. This tool offers an interactive, single-pane dashboard that aggregates metadata from all corners of your organization's environment and allows localized changes to be performed directly in real time.")
    
    add_body_text(doc, "Key highlights of Dataverse Label Translator:")
    add_body_text(doc, "Allows translation of forms, views, fields, entity names, sitemaps, global option sets, and more in one unified grid.", bold_prefix="Comprehensive UI Coverage: ", bullet=True)
    add_body_text(doc, "Allows translation of all entity-dependent metadata items without switching tabs.", bold_prefix="All-In-One Translation Mode: ", bullet=True)
    add_body_text(doc, "Speeds up workflows using Google Gemini, OpenAI, or Azure OpenAI APIs to automatically generate translations.", bold_prefix="AI-Assisted Automated Translations: ", bullet=True)
    add_body_text(doc, "Stores a persistent glossary of corporate terms directly inside Dataverse, letting users run matching translations locally in bulk.", bold_prefix="Translation Dictionary: ", bullet=True)
    add_body_text(doc, "Simplifies workflow by grouping components belonging to specific custom solutions.", bold_prefix="Solution Filtering: ", bullet=True)
    
    add_callout(doc, "Anh Phước hãy mở Power Apps portal hoặc app của anh, chụp hình Icon hoặc giao diện chính và thay thế vào đây.\nHướng dẫn chèn ảnh: Click vào đây -> Insert -> Pictures -> Chọn ảnh đã chụp.", "📷 HÌNH ẢNH: LOGO / GIAO DIỆN CHÀO MỪNG CỦA APP", COLOR_SCREENSHOT_BG, is_screenshot=True)
    
    add_heading_1(doc, "2. Requirements & Installation")
    add_heading_2(doc, "2.1 System Requirements")
    add_body_text(doc, "To import and run the application successfully, ensure the following requirements are met:")
    add_body_text(doc, "Microsoft Dynamics CRM 2016 (v8.0) or later, or any modern Microsoft Dataverse environment (Power Apps).", bullet=True)
    add_body_text(doc, "A System Administrator or System Customizer security role is required to modify entity metadata, forms, and views.", bullet=True)
    add_body_text(doc, "Multiple languages must be enabled and provisioned in the Dataverse environment to display localization columns.", bullet=True)
    
    add_heading_2(doc, "2.2 Installation Steps")
    add_body_text(doc, "1. Download the Dataverse Label Translator managed solution zip file from AppSource or the release package.")
    add_body_text(doc, "2. Navigate to the Power Apps maker portal (https://make.powerapps.com).")
    add_body_text(doc, "3. Select your target Environment in the top-right corner.")
    add_body_text(doc, "4. Click on Solutions in the left navigation pane.")
    add_body_text(doc, "5. Click Import solution, select the zip file, and click Next.")
    add_body_text(doc, "6. Confirm the connections and parameters, then click Import. The system will start importing the solution named DataverseLabelTranslator.")
    add_body_text(doc, "7. Once import is complete, click Publish All Customizations to ensure all components are active.")
    
    add_heading_2(doc, "2.3 Accessing the Application")
    add_body_text(doc, "After successful import, the application is accessible as a model-driven app:")
    add_body_text(doc, "1. In the Power Apps maker portal, go to Apps.")
    add_body_text(doc, "2. Locate Dataverse Label Translator and click Play.")
    add_body_text(doc, "3. The app will launch, loading the primary dashboard hosted at the web resource pl_/DataverseLabelTranslator/html/App.html.")
    
    add_callout(doc, "Anh Phước hãy chụp ảnh danh sách App trong Power Apps portal, chỉ mũi tên vào app 'Dataverse Label Translator' để người dùng dễ nhìn thấy cách mở app.", "📷 HÌNH ẢNH: HƯỚNG DẪN MỞ APP", COLOR_SCREENSHOT_BG, is_screenshot=True)
    
    add_heading_1(doc, "3. Interface Overview")
    add_body_text(doc, "The application features an intuitive single-page interface powered by w2ui 2.0. The layout is optimized to display complex data grids with horizontal scrolling to support as many language columns as you have installed in your environment.")
    
    add_heading_2(doc, "3.1 Component Guide")
    add_body_text(doc, "The main workspace is divided into three key areas:")
    add_body_text(doc, "Contains configuration options including Solution dropdown, Entity picker, Component Type dropdown, and action buttons like Load, Save, App Settings, and Manage Dictionary.", bold_prefix="1. Top Control Bar: ", bullet=True)
    add_body_text(doc, "Displays loaded translation records with columns for CRM unique key, type description, source base language text, and editable text fields for each installed language in your Dataverse environment.", bold_prefix="2. Main Translation Grid: ", bullet=True)
    add_body_text(doc, "Provides contextual search, regex filters, toggles to show only untranslated/missing fields, grid statistics, and lock language controls.", bold_prefix="3. Grid Helper Panel (Toolbar): ", bullet=True)
    
    add_callout(doc, "Anh Phước hãy mở app, load một entity bất kỳ (ví dụ Account), chụp toàn bộ màn hình giao diện chính với đầy đủ control panel và bảng lưới (Grid) hiển thị dữ liệu để paste vào đây.", "📷 HÌNH ẢNH: TỔNG QUAN GIAO DIỆN CHÍNH CỦA ỨNG DỤNG", COLOR_SCREENSHOT_BG, is_screenshot=True)
    
    add_heading_1(doc, "4. Translation Scope & Component Types")
    add_body_text(doc, "Dataverse Label Translator supports translation of 13 main component types, including an All-In-One mode. Below is the list of supported types and what each translates:")
    
    table = doc.add_table(rows=1, cols=3)
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    
    hdr_cells = table.rows[0].cells
    hdr_cells[0].text = 'Type / Component'
    hdr_cells[1].text = 'Scope'
    hdr_cells[2].text = 'Translated Labels'
    
    for cell in hdr_cells:
        set_cell_background(cell, HEX_PRIMARY)
        set_cell_margins(cell, top=120, bottom=120, left=150, right=150)
        for p in cell.paragraphs:
            for r in p.runs:
                r.font.bold = True
                r.font.name = 'Segoe UI'
                r.font.size = Pt(10)
                r.font.color.rgb = RGBColor(255, 255, 255)
                
    types_data = [
        ("0. All-In-One", "Entity", "Combines all entity-dependent types into one grid for bulk operations."),
        ("1. Attributes", "Entity", "Display Names and Descriptions of fields."),
        ("2. Option Sets", "Entity", "Local option set text labels, including state/status code values."),
        ("3. Forms", "Entity", "Labels of tabs, sections, fields, header, and footer controls inside entity forms."),
        ("4. Views", "Entity", "Display names and descriptions of system and public views."),
        ("5. Form Metadata", "Entity", "Display names of the form records themselves."),
        ("6. Entity Metadata", "Entity", "Display names, plural collection names, and descriptions of the entity."),
        ("7. Relationships", "Entity", "Display names of entity relationships used in navigation menus."),
        ("8. Charts", "Entity", "Display names of visualizations associated with the entity."),
        ("9. Business Process Flows", "Entity", "Labels for stages and steps of BPFs."),
        ("10. Sitemap", "None", "Display labels for navigation areas, groups, and subareas in sitemaps."),
        ("11. Dashboards", "None", "System and user dashboard tab/control labels."),
        ("12. Web Resources", "None", "Text strings/labels contained in files such as HTML or XML."),
        ("13. Global Option Sets", "None", "Display labels of option set choices shared globally across entities."),
        ("14. Content Snippets", "Special", "Legacy Dynamics Portal/Power Pages content snippets (visible only for adx_contentsnippet entity).")
    ]
    
    for idx, (t, s, d) in enumerate(types_data):
        row_cells = table.add_row().cells
        row_cells[0].text = t
        row_cells[1].text = s
        row_cells[2].text = d
        
        bg_color = HEX_LIGHT_GRAY if idx % 2 == 0 else "FFFFFF"
        for i, cell in enumerate(row_cells):
            set_cell_background(cell, bg_color)
            set_cell_margins(cell, top=100, bottom=100, left=150, right=150)
            for p in cell.paragraphs:
                for r in p.runs:
                    r.font.name = 'Segoe UI'
                    r.font.size = Pt(9.5)
                    r.font.color.rgb = COLOR_BODY
                    if i == 0:
                        r.font.bold = True
                        
    p_space = doc.add_paragraph()
    p_space.paragraph_format.space_before = Pt(12)
    
    add_heading_1(doc, "5. Translation Workflows")
    add_heading_2(doc, "5.1 Manual Inline Translation")
    add_body_text(doc, "The simplest workflow is manual editing, which is ideal for single label overrides or quick corrections:")
    add_body_text(doc, "Open the Dataverse Label Translator app.", bullet=True)
    add_body_text(doc, "Select a Solution (e.g., Default Solution) to filter your list.", bullet=True)
    add_body_text(doc, "Select an Entity from the dropdown (or select 'None' for sitemaps, dashboards, web resources, etc.).", bullet=True)
    add_body_text(doc, "Select the target Component Type (e.g., 1. Attributes) and click Load. The grid is populated with metadata keys and localized columns.", bullet=True)
    add_body_text(doc, "Double-click any cell in the language columns (e.g., Vietnamese or French) to edit the text inline.", bullet=True)
    add_body_text(doc, "Modified cells will highlight with a dirty indicator. You can continue editing as many rows as needed.", bullet=True)
    add_body_text(doc, "Click Save in the top toolbar. The app communicates with Dataverse APIs using WebApiClient to save the changes and publish them automatically.", bullet=True)
    
    add_callout(doc, "Anh Phước hãy chụp ảnh cận cảnh một dòng trong grid đang được double-click edit, hiển thị ô nhập chữ và nút Save ở trên toolbar để người dùng dễ làm theo.", "📷 HÌNH ẢNH: HƯỚNG DẪN EDIT CELL INLINE VÀ SAVE", COLOR_SCREENSHOT_BG, is_screenshot=True)
    
    add_heading_2(doc, "5.2 All-In-One Mode (Bulk Translation)")
    add_body_text(doc, "Instead of loading and saving individual component types (Attributes, Forms, Views, etc.) separately, you can select the 0. All-In-One option. This loads all entity-specific labels into a single grid, making it much faster to translate a new custom entity end-to-end and submit the updates in a single batch.")
    
    add_heading_2(doc, "5.3 Grid Filters & Search Helpers")
    add_body_text(doc, "To handle large entities with thousands of fields and labels, the app provides tools to focus on what matters:")
    add_body_text(doc, "Type search terms into the toolbar's search box. The grid filters instantly to match your search text in either the unique CRM key or the source language column.", bold_prefix="Instant Search: ", bullet=True)
    add_body_text(doc, "Check this checkbox to hide all rows that already have complete translations across all language columns. This is highly useful for locating untranslated gaps.", bold_prefix="Show Untranslated Records Only: ", bullet=True)
    add_body_text(doc, "Prevents accidental edits to columns that are already validated or translated by lock-protecting them.", bold_prefix="Lock Columns: ", bullet=True)
    
    add_heading_1(doc, "6. AI-Assisted Translation")
    add_body_text(doc, "Dataverse Label Translator integrates with state-of-the-art LLMs to translate labels in bulk. Rather than typing translations row-by-row, you can let the AI generate high-quality translation proposals for all empty cells in seconds.")
    
    add_heading_2(doc, "6.1 Setting Up AI Provider Credentials")
    add_body_text(doc, "1. Click on the App Settings button in the top toolbar to open the settings dialog.")
    add_body_text(doc, "2. Select your preferred AI Provider:")
    add_body_text(doc, "Requires a Google Gemini API Key. Uses high-performing Gemini models with direct prompt engineering optimized for software terminology.", bold_prefix="- Google Gemini: ", bullet=True)
    add_body_text(doc, "Requires your endpoint URL, model name, and API key. Works with OpenAI chat-completions endpoints.", bold_prefix="- OpenAI: ", bullet=True)
    add_body_text(doc, "Connects to Azure AI endpoints using your API key.", bold_prefix="- Azure: ", bullet=True)
    add_body_text(doc, "3. Enter your API Key and customize the target Model name if desired.")
    add_body_text(doc, "4. (Optional) Custom Prompt: You can write instructions for the AI (e.g., 'Translate technical terms literally, do not translate acronyms, use formal Vietnamese').")
    add_body_text(doc, "5. Click Save. Your credentials are saved in the Dataverse app settings web resource for this environment.")
    
    add_callout(doc, "Anh Phước hãy click mở nút App Settings trên toolbar, chụp ảnh màn hình Popup điền API Key và cấu hình App Settings để paste vào đây.", "📷 HÌNH ẢNH: HƯỚNG DẪN CẤU HÌNH APP SETTINGS", COLOR_SCREENSHOT_BG, is_screenshot=True)
    
    add_heading_2(doc, "6.2 Running Auto Translate")
    add_body_text(doc, "Once configured, you can auto-translate loaded labels:")
    add_body_text(doc, "1. On the main toolbar, select the Source Language and Target Language columns.")
    add_body_text(doc, "2. Click the Auto Translate button. The dialog will open.")
    add_body_text(doc, "3. Choose the execution mode:")
    add_body_text(doc, "Translates only empty cells in the target language column.", bold_prefix="- Missing values: ", bullet=True)
    add_body_text(doc, "Translates all loaded rows, overwriting existing translations.", bold_prefix="- Overwrite all: ", bullet=True)
    add_body_text(doc, "Translates only the rows currently highlighted in the grid.", bold_prefix="- Selected records: ", bullet=True)
    add_body_text(doc, "4. Click Translate. The app batches the translation request, contacts the AI provider, and loads the translations as proposed text.")
    add_body_text(doc, "5. Review the proposed translations, make manual edits if needed, and click Save to write the translations to Dataverse.")
    
    add_callout(doc, "Anh Phước hãy bấm vào Auto Translate, chụp ảnh Popup cấu hình dịch tự động (chọn nguồn, đích và chế độ dịch) để paste vào đây.", "📷 HÌNH ẢNH: DIALOG CẤU HÌNH AUTO TRANSLATE", COLOR_SCREENSHOT_BG, is_screenshot=True)
    
    add_heading_1(doc, "7. Translation Dictionary (Glossary)")
    add_body_text(doc, "The Translation Dictionary acts as a local term store or glossary. For terms unique to your business, you can register translations in the Dictionary so they are translated consistently across forms, attributes, views, and dashboards without querying AI.")
    
    add_heading_2(doc, "7.1 How the Dictionary Works")
    add_body_text(doc, "The dictionary is saved and shared directly within your environment using a separate Dataverse-backed XML resource.")
    add_body_text(doc, "Dataverse-Backed: Saved as pl_/DataverseLabelTranslator/data/TranslationDictionary.xml inside an automatically created unmanaged solution named Dataverse Label Translator Data (DataverseLabelTranslatorData).", bullet=True)
    add_body_text(doc, "Base Language Matching: The key is always matched against the environment's base language.", bullet=True)
    
    add_heading_2(doc, "7.2 Managing Dictionary Entries")
    add_body_text(doc, "You can access and update dictionary entries using two methods:")
    add_body_text(doc, "Click the Manage Dictionary button in the top toolbar to open the dictionary editor. Click Save to save the dictionary back to Dataverse.", bold_prefix="Method 1: Dictionary Dialog. ", bullet=False)
    add_body_text(doc, "If you have manually translated a row in the main grid and want to save it as an approved term, select the row, click the Add Selected to Dictionary button.", bold_prefix="Method 2: Save directly from Grid. ", bullet=False)
    
    add_callout(doc, "Anh Phước hãy bấm vào Manage Dictionary, chụp ảnh Popup hiển thị danh sách từ vựng đã lưu trong Dictionary để chèn vào đây.", "📷 HÌNH ẢNH: QUẢN LÝ TỪ ĐIỂN TRANSLATION DICTIONARY", COLOR_SCREENSHOT_BG, is_screenshot=True)
    
    add_heading_1(doc, "8. Important Considerations & Troubleshooting")
    add_heading_2(doc, "8.1 Form Translation Behavior")
    add_body_text(doc, "Dataverse forms only return localized labels for the current user's UI language. To retrieve and write translations for all installed languages, the Dataverse Label Translator performs the following automated steps during Form loading and saving:")
    add_body_text(doc, "1. Reads the list of installed language LCIDs.", bullet=True)
    add_body_text(doc, "2. Temporarily changes the current user's language settings in Dataverse to each target language in sequence to read/write the labels.", bullet=True)
    add_body_text(doc, "3. Reverts the user's language settings back to the original base language.", bullet=True)
    
    add_callout(doc, "Avoid refreshing the browser, closing the browser window, or navigating away while the Form Loader is running. If the operation is interrupted, your personal Dataverse UI language may remain changed. If this happens, you can manually restore your UI language in the Dynamics 365 Personal Options menu.", "⚠️ WARNING: INTERRUPTED FORM TRANSLATION", "FDF2E9")
    
    add_heading_2(doc, "8.2 Overridden Attribute Labels in Forms")
    add_body_text(doc, "Sometimes, translating field attributes does not update the text displayed on a form. This is because Dataverse forms support custom labels that override default attribute labels.")
    add_body_text(doc, "If a form shows a custom label overriding the attribute, use the Remove Overridden Attribute Labels button within the form handler. This clears the form-level override and forces the form to display the default attribute translation you configured.")
    
    add_callout(doc, "It is highly recommended to export a backup solution containing the target entity forms before executing the 'Remove Overridden Attribute Labels' command.", "💡 BEST PRACTICE: EXPORT BACKUP", "E8F8F5")
    
    version = "1.0.0.0"
    output_dir = rf"d:\github\DataverseLabelTranslator\release\{version}\appsource\Documents"
    os.makedirs(output_dir, exist_ok=True)
    file_path = os.path.join(output_dir, f"UserGuide.{version}.docx")
    doc.save(file_path)
    print(f"Document saved successfully at: {file_path}")

if __name__ == "__main__":
    build_document()
