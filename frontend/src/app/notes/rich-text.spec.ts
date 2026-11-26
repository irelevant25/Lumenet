import { USUAL_COLOR_MARK, cleanNotes, hexColor } from './rich-text';

describe('the notes of a project', () => {
  it('keep the marks of the editor: bold, underline, a size, a color and lists', () => {
    const notes =
      '<div><b>Cue 1</b> when the <u>house lights</u> are out</div>' +
      '<div><font size="5">Big</font> and <font color="#ff5247">red</font></div>' +
      '<ul><li>one</li><li>two</li></ul><ol><li>first</li></ol><p>A line<br>and another</p>';
    expect(cleanNotes(notes)).toBe(notes);
  });

  it('take out every other element and keep its text; a block of another kind is a line', () => {
    expect(cleanNotes('<h1>Title</h1><span>a</span> <i>b</i> <a href="https://example.org">c</a>')).toBe('<div>Title</div>a b c');
    expect(cleanNotes('<strong>bold</strong>')).toBe('<b>bold</b>');
    expect(cleanNotes('<table><tr><td>one</td><td>two</td></tr></table>')).toBe('<div>onetwo</div>');
  });

  it('take out what is no text at all, with its content, and every attribute but the marks', () => {
    const hostile =
      '<div onclick="steal()">text<script>steal()</script><img src=x onerror="steal()"><style>*{}</style></div>' +
      '<iframe src="https://example.org"></iframe><svg><text>drawn</text></svg><b onmouseover="steal()" class="x">bold</b>' +
      '<!-- a comment --><font color="red" size="9" face="Arial" onload="steal()">plain</font>';
    expect(cleanNotes(hostile)).toBe('<div>text</div><b>bold</b>plain');
  });

  it('keep a color as #rrggbb and a size from 1 to 7, but not the usual ones', () => {
    expect(cleanNotes('<font color="#ABC">a</font>')).toBe('<font color="#aabbcc">a</font>');
    expect(cleanNotes('<font color="rgb(255, 82, 71)">a</font>')).toBe('<font color="#ff5247">a</font>');
    expect(cleanNotes(`<font color="${USUAL_COLOR_MARK}">usual</font>`)).toBe('usual');
    expect(cleanNotes('<font size="3">usual</font>')).toBe('usual');
    expect(cleanNotes('<font size="6" color="javascript:steal()">huge</font>')).toBe('<font size="6">huge</font>');
  });

  it('turn a style a browser writes into the marks: bold, underline and a color', () => {
    expect(cleanNotes('<span style="font-weight: 700; color: rgb(61, 220, 132);">go</span>')).toBe(
      '<b><font color="#3ddc84">go</font></b>',
    );
    expect(cleanNotes('<span style="text-decoration: underline;">under</span>')).toBe('<u>under</u>');
    expect(cleanNotes('<div style="font-size: 30px; background: url(x)">plain</div>')).toBe('<div>plain</div>');
  });

  it('are empty without text, as after everything was deleted', () => {
    expect(cleanNotes('')).toBe('');
    expect(cleanNotes('<br>')).toBe('');
    expect(cleanNotes('<div><br></div><div> </div>')).toBe('');
    expect(cleanNotes('<script>alert(1)</script>')).toBe('');
  });

  it('know a color in its three ways of writing', () => {
    expect(hexColor('#FFB020')).toBe('#ffb020');
    expect(hexColor('#fb0')).toBe('#ffbb00');
    expect(hexColor('rgba(255, 176, 32, 0.5)')).toBe('#ffb020');
    expect(hexColor('rgb(300, 0, 0)')).toBeNull();
    expect(hexColor('red')).toBeNull();
  });
});
