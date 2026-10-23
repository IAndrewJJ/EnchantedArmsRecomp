"""EnchantedArmsPC Linux launcher; the game starts only after Play is clicked."""
import argparse
import json
import os
from pathlib import Path
import signal
import sys
import time

import core

def worker(arguments):
    if os.getpgrp() != os.getpid():
        os.setsid()
    try:
        core.build(json.loads(Path(arguments.request).read_text()))
        return 0
    except Exception as error:
        import traceback
        traceback.print_exc()
        print('SETUP FAILED: ' + str(error), flush=True)
        return 1

def gui(arguments):
    from PySide6.QtCore import Qt, QProcess, QTimer, QUrl
    from PySide6.QtGui import QIcon, QPixmap, QDesktopServices
    from PySide6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout,
        QHBoxLayout, QLabel, QPushButton, QDialog, QTabWidget, QFormLayout,
        QComboBox, QCheckBox, QSpinBox, QLineEdit, QFileDialog, QMessageBox,
        QDialogButtonBox, QScrollArea, QProgressBar)

    application = QApplication(sys.argv[:1])
    application.setApplicationName('EnchantedArmsPC')
    application.setOrganizationName('EnchantedArmsPC')
    icon = core.resources() / 'icon.png'
    if not icon.exists():
        icon = Path(__file__).resolve().parents[1] / 'launcher/branding/EnchantedArmsPC.png'
    application.setWindowIcon(QIcon(str(icon)))
    application.setStyle('Fusion')
    application.setStyleSheet('''
        QWidget { background: #091421; color: #d9e8e9; font: 10pt "DejaVu Sans"; }
        QMainWindow, QDialog { background: #091421; }
        QLabel#title { color: #d4b778; font: 27pt "DejaVu Serif"; }
        QLabel#subtitle { color: #7bb6c2; }
        QLabel#status { color: #80dce0; font-size: 12pt; }
        QPushButton { background: #102533; border: 1px solid #345666; padding: 10px 16px; min-height: 18px; }
        QPushButton:hover { background: #1a3f4d; border-color: #7ac6cc; }
        QPushButton:disabled { color: #617480; border-color: #253b47; }
        QPushButton#play { background: #205b68; border-color: #79ccd1; font-size: 13pt; padding: 18px; }
        QLineEdit, QComboBox, QSpinBox { background: #122635; border: 1px solid #355260; padding: 7px; }
        QComboBox QAbstractItemView { background: #122635; selection-background-color: #205b68; }
        QTabWidget::pane { border: 1px solid #2b4857; }
        QTabBar::tab { background: #102533; padding: 12px; margin-right: 3px; }
        QTabBar::tab:selected { background: #20505f; color: #b9f0ef; }
        QCheckBox { spacing: 9px; padding: 5px 0; }
        QProgressBar { border: none; background: #122635; max-height: 4px; }
        QProgressBar::chunk { background: #77ced1; }
        QScrollArea { border: none; }
        QToolTip { background: #122635; color: #d9e8e9; border: 1px solid #77ced1; }
    ''')

    class Settings(QDialog):
        def __init__(self, previous, running=False):
            super().__init__()
            self.previous = previous
            self.values = core.normalize(previous)
            self.build_requested = False
            self.setWindowTitle('EnchantedArmsPC · Settings')
            self.resize(780, 640)
            layout = QVBoxLayout(self)
            layout.setContentsMargins(24, 24, 24, 20)
            heading = QLabel('Settings')
            heading.setObjectName('title')
            layout.addWidget(heading)
            layout.addWidget(QLabel('Changes apply the next time you play.'))
            self.tabs = QTabWidget()
            layout.addWidget(self.tabs)
            self.fields = {}
            game = self.page('Game')
            self.iso = self.location(game, 'Game ISO', self.values['Iso'], iso=True)
            self.root = self.location(game, 'Build location', str(core.build_root(self.values)), enabled=not running)
            self.save = self.location(game, 'Save location', str(core.save_root(self.values)), enabled=not running)
            self.default_save = not bool(self.values['SaveLocation'])
            self.root.textChanged.connect(lambda: self.save.setText(str(Path(self.root.text())/'Saves')) if self.default_save else None)
            self.save.textEdited.connect(lambda: setattr(self, 'default_save', False))
            restore = QPushButton('Use default save location')
            restore.setEnabled(not running)
            restore.clicked.connect(self.restore_saves)
            game.addRow('', restore)
            note = QLabel('Saves default to the build folder. When you choose another save folder, existing progress is copied without overwriting different saves.')
            note.setWordWrap(True)
            game.addRow(note)
            build_button = QPushButton('Rebuild game' if core.ready(self.values) else 'Build game')
            build_button.setEnabled(not running)
            build_button.clicked.connect(self.request_build)
            game.addRow('', build_button)
            shortcut = QPushButton('Create desktop shortcut')
            shortcut.clicked.connect(self.shortcut)
            game.addRow('', shortcut)
            graphics = self.page('Graphics')
            self.choice(graphics, 'Render resolution', 'Scale', [('1× · Native',1), ('2×',2), ('3×',3), ('4×',4)])
            self.choice(graphics, 'Upscaling', 'Upscaler', [('Original scaling','bilinear'), ('AMD FSR 1','fsr'), ('AMD FidelityFX CAS','cas')])
            self.choice(graphics, 'Antialiasing', 'PostAA', [('Original','none'), ('FXAA','fxaa'), ('FXAA strong','fxaa_extreme'), ('Supersampling + FXAA','ssaa')])
            self.choice(graphics, 'Texture filtering', 'Anisotropy', [('Original',-1), ('Off',0), ('1×',1), ('2×',2), ('4×',3), ('8×',4), ('16×',5)])
            self.choice(graphics, 'Texture detail', 'TextureDetail', [('Original',0), ('Crisp',1), ('Strong',2)])
            self.number(graphics, 'Sharpening', 'Sharpness', 0, 100)
            self.check(graphics, "Keep the game's native antialiasing", 'NativeMsaa')
            screen = self.page('Display')
            self.choice(screen, 'Display mode', 'Fullscreen', [('Windowed',False), ('Borderless fullscreen',True)])
            self.choice(screen, 'Window size', 'Width', [('1280 × 720',1280), ('1920 × 1080',1920), ('2560 × 1440',2560), ('3840 × 2160',3840)])
            self.choice(screen, 'Frame rate', 'FrameLimit', [('30 FPS',30), ('60 FPS',60)])
            self.check(screen, 'Preserve aspect ratio', 'KeepAspect')
            self.check(screen, 'Reduce gradient banding', 'Dither')
            self.check(screen, 'Show frame-time graph with the F6 counter', 'FrameTimeGraph')
            controls = self.page('Controls')
            self.check(controls, 'Mouse camera · F2 releases the mouse', 'MouseCamera')
            self.check(controls, 'Invert camera Y', 'InvertCameraY')
            self.check(controls, 'Swap triggers', 'SwapTriggers')
            for caption, key, low, high in [('Mouse sensitivity','MouseSensitivity',1,1000),
                ('Left stick deadzone','LeftDeadzone',0,40), ('Right stick deadzone','RightDeadzone',0,40),
                ('Camera response','CameraResponse',25,200)]:
                self.number(controls, caption, key, low, high)
            self.keys = []
            for name, value in zip(core.CONTROL_NAMES, self.values['Keys']):
                edit = QLineEdit(value)
                edit.setMaxLength(100)
                controls.addRow(name, edit)
                self.keys.append(edit)
            updates = self.page('Updates')
            label = QLabel('Linux preview ' + core.VERSION + '\n\nDownload Linux updates from the project’s release page. Your built game and saves stay in their chosen folders.')
            label.setWordWrap(True)
            updates.addRow(label)
            releases = QPushButton('Open Linux releases')
            releases.clicked.connect(lambda: QDesktopServices.openUrl(QUrl('https://github.com/IAndrewJJ/EnchantedArmsRecomp/releases?q=linux-v')))
            updates.addRow(releases)
            buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Apply | QDialogButtonBox.StandardButton.Cancel)
            buttons.button(QDialogButtonBox.StandardButton.Apply).clicked.connect(self.apply)
            buttons.rejected.connect(self.reject)
            layout.addWidget(buttons)

        def page(self, name):
            scroll = QScrollArea()
            scroll.setWidgetResizable(True)
            body = QWidget()
            form = QFormLayout(body)
            form.setContentsMargins(20, 20, 20, 20)
            form.setSpacing(14)
            scroll.setWidget(body)
            self.tabs.addTab(scroll, name)
            return form

        def location(self, form, name, text, iso=False, enabled=True):
            row = QWidget()
            box = QHBoxLayout(row)
            box.setContentsMargins(0,0,0,0)
            edit = QLineEdit(text)
            edit.setAccessibleName(name)
            edit.setEnabled(enabled)
            button = QPushButton('Choose ISO…' if iso else 'Choose folder…')
            button.setEnabled(enabled)
            def choose():
                value = (QFileDialog.getOpenFileName(self, 'Choose Enchanted Arms', str(Path.home()), 'Xbox 360 ISO (*.iso *.ISO)')[0]
                         if iso else QFileDialog.getExistingDirectory(self, 'Choose ' + name.lower(), edit.text()))
                if value:
                    if name == 'Save location':
                        self.default_save = False
                    edit.setText(value)
            button.clicked.connect(choose)
            box.addWidget(edit, 1)
            box.addWidget(button)
            form.addRow(name, row)
            return edit

        def choice(self, form, caption, key, options):
            control = QComboBox()
            for text, value in options:
                control.addItem(text, value)
            control.setCurrentIndex(next(i for i, (_, value) in enumerate(options) if value == self.values[key]))
            form.addRow(caption, control)
            self.fields[key] = control

        def number(self, form, caption, key, low, high):
            control = QSpinBox()
            control.setRange(low, high)
            control.setValue(self.values[key])
            form.addRow(caption, control)
            self.fields[key] = control

        def check(self, form, caption, key):
            control = QCheckBox(caption)
            control.setChecked(self.values[key])
            form.addRow(control)
            self.fields[key] = control

        def restore_saves(self):
            self.default_save = True
            self.save.setText(str(Path(self.root.text())/'Saves'))

        def read(self):
            values = dict(self.values, Iso=self.iso.text(), BuildLocation=self.root.text(),
                          SaveLocation='' if self.default_save else self.save.text())
            for key, control in self.fields.items():
                values[key] = control.currentData() if isinstance(control, QComboBox) else (control.isChecked() if isinstance(control, QCheckBox) else control.value())
            values['Keys'] = [edit.text() for edit in self.keys]
            return core.normalize(values)

        def apply(self):
            try:
                self.values = core.save_settings(self.read(), self.previous)
                self.accept()
            except Exception as error:
                QMessageBox.information(self, 'Check your settings', str(error))

        def request_build(self):
            if not Path(self.iso.text()).is_file():
                QMessageBox.information(self, 'Choose your game', 'Choose an existing Enchanted Arms ISO first.')
                return
            self.build_requested = True
            self.apply()

        def shortcut(self):
            try:
                self.values = core.save_settings(self.read(), self.previous)
                path = core.desktop_shortcut(self.values)
                QMessageBox.information(self, 'Desktop shortcut', 'Created ' + str(path))
            except Exception as error:
                QMessageBox.information(self, 'Desktop shortcut', str(error))

    class Launcher(QMainWindow):
        def __init__(self):
            super().__init__()
            self.setWindowTitle('EnchantedArmsPC')
            self.resize(880, 540)
            try:
                self.settings = core.load_settings()
            except Exception as error:
                self.settings = core.normalize({})
                QMessageBox.information(self, 'Settings could not be read', str(error))
            self.game = None
            self.worker = None
            self.log = None
            self.output = ''
            self.failure = None
            self.cancelled = False
            body = QWidget()
            self.setCentralWidget(body)
            layout = QVBoxLayout(body)
            layout.setContentsMargins(44, 38, 44, 24)
            brand = QHBoxLayout()
            emblem = QLabel()
            emblem.setPixmap(QPixmap(str(icon)).scaled(100,100,Qt.AspectRatioMode.KeepAspectRatio,Qt.TransformationMode.SmoothTransformation))
            brand.addWidget(emblem)
            heading = QVBoxLayout()
            title = QLabel('EnchantedArmsPC')
            title.setObjectName('title')
            heading.addWidget(title)
            subtitle = QLabel('Your journey, reawakened.')
            subtitle.setObjectName('subtitle')
            heading.addWidget(subtitle)
            brand.addLayout(heading, 1)
            layout.addLayout(brand)
            layout.addStretch(1)
            self.status = QLabel()
            self.status.setObjectName('status')
            self.status.setWordWrap(True)
            layout.addWidget(self.status)
            self.play = QPushButton()
            self.play.setObjectName('play')
            self.play.clicked.connect(self.play_clicked)
            layout.addWidget(self.play)
            self.progress = QProgressBar()
            self.progress.setRange(0,0)
            self.progress.hide()
            layout.addWidget(self.progress)
            row = QHBoxLayout()
            self.settings_button = QPushButton('Settings')
            self.settings_button.clicked.connect(self.open_settings)
            row.addWidget(self.settings_button)
            self.cancel_button = QPushButton('Cancel setup')
            self.cancel_button.clicked.connect(self.cancel_build)
            self.cancel_button.hide()
            row.addWidget(self.cancel_button)
            row.addStretch(1)
            layout.addLayout(row)
            layout.addStretch(1)
            footer = QHBoxLayout()
            footer.addWidget(QLabel('F6  FPS counter     ·     F8  Screenshot'))
            footer.addStretch(1)
            footer.addWidget(QLabel('Linux preview ' + core.VERSION))
            layout.addLayout(footer)
            self.timer = QTimer(self)
            self.timer.setInterval(1000)
            self.timer.timeout.connect(self.check_game)
            self.refresh()

        def refresh(self):
            running = self.game is not None and self.game.poll() is None
            self.play.setEnabled(not running and self.worker is None)
            self.settings_button.setEnabled(self.worker is None)
            self.play.setText('GAME RUNNING' if running else ('PLAY ENCHANTED ARMS' if core.ready(self.settings) else 'SET UP GAME'))
            self.status.setText('Your journey is in progress' if running else ('Ready to play' if core.ready(self.settings) else 'Choose your game disc to begin'))

        def open_settings(self):
            dialog = Settings(self.settings, self.game is not None and self.game.poll() is None)
            if dialog.exec() == QDialog.DialogCode.Accepted:
                self.settings = dialog.values
                self.refresh()
                if dialog.build_requested:
                    self.start_build()

        def play_clicked(self):
            if not core.ready(self.settings):
                self.open_settings()
                return
            try:
                self.game = core.launch_game(self.settings)
                self.timer.start()
                self.refresh()
            except Exception as error:
                QMessageBox.information(self, 'Game could not start', str(error))

        def check_game(self):
            if self.game is not None and self.game.poll() is not None:
                code = self.game.returncode
                self.game = None
                self.timer.stop()
                self.refresh()
                if code:
                    QMessageBox.information(self, 'Game closed unexpectedly',
                        'The game closed with code ' + str(code) + '.\n\nThe newest boot log is in ' +
                        str(core.build_root(self.settings)/'.local/runtime'))

        def start_build(self):
            try:
                root = core.reserve_build_root(core.build_root(self.settings))
            except Exception as error:
                QMessageBox.information(self, 'Build folder unavailable', str(error))
                return
            logs = root / '.local/logs'
            logs.mkdir(parents=True, exist_ok=True)
            self.log_path = logs / time.strftime('setup-linux-%Y%m%dT%H%M%SZ.log', time.gmtime())
            self.log = self.log_path.open('w', encoding='utf-8')
            request = root / '.local/linux-build-request.json'
            core.atomic_json(request, self.settings)
            self.worker = QProcess(self)
            self.worker.setProcessChannelMode(QProcess.ProcessChannelMode.MergedChannels)
            self.worker.readyReadStandardOutput.connect(self.read_output)
            self.worker.finished.connect(self.build_finished)
            self.worker.errorOccurred.connect(self.worker_error)
            self.failure = None
            self.output = ''
            self.cancelled = False
            self.progress.show()
            self.cancel_button.show()
            self.refresh()
            self.status.setText('Preparing your game…')
            self.play.setText('PREPARING YOUR GAME')
            args = ['--worker', '--request', str(request)]
            if not getattr(sys, 'frozen', False):
                args.insert(0, str(Path(__file__).resolve()))
            self.worker.start(sys.executable, args)

        def worker_error(self, error):
            if error == QProcess.ProcessError.FailedToStart and self.worker is not None:
                self.failure = 'The setup worker could not start: ' + self.worker.errorString()
                self.build_finished(-1, QProcess.ExitStatus.CrashExit)

        def read_output(self):
            data = bytes(self.worker.readAllStandardOutput()).decode('utf-8', errors='replace')
            if self.log and self.log.tell() < 16*1024*1024:
                self.log.write(data)
                self.log.flush()
            self.output += data
            lines = self.output.split('\n')
            self.output = lines.pop()
            for line in lines:
                if line.startswith('STAGE: '):
                    self.status.setText(line[7:])
                elif line.startswith('SETUP FAILED: '):
                    self.failure = line[14:]

        def build_finished(self, code, status):
            self.read_output()
            self.worker.deleteLater()
            self.worker = None
            if self.log:
                self.log.close()
                self.log = None
            self.progress.hide()
            self.cancel_button.hide()
            self.refresh()
            if code != 0 and not self.cancelled:
                QMessageBox.information(self, 'Setup incomplete', (self.failure or 'Game setup could not finish.') + '\n\nDetails: ' + str(self.log_path))

        def cancel_build(self):
            if self.worker is not None:
                self.cancelled = True
                try:
                    os.killpg(self.worker.processId(), signal.SIGTERM)
                except ProcessLookupError:
                    self.worker.kill()
                self.status.setText('Stopping setup…')

        def closeEvent(self, event):
            if self.worker is not None:
                answer = QMessageBox.question(self, 'Setup is running', 'Cancel setup and close the launcher?')
                if answer != QMessageBox.StandardButton.Yes:
                    event.ignore()
                    return
                self.cancel_build()
                if not self.worker.waitForFinished(5000):
                    self.worker.kill()
            event.accept()

    window = Launcher()
    if arguments.smoke_test:
        # This route is used by Linux CI with Qt's offscreen platform. It never
        # selects an ISO, starts the game, writes settings or creates a shortcut.
        dialog = Settings(core.normalize({}))
        assert dialog.read()['FrameLimit'] == 30
        assert dialog.tabs.count() == 5
        dialog.close()
        if arguments.preview:
            window.show()
            application.processEvents()
            window.grab().save(arguments.preview)
        window.close()
        print('PASS: Linux launcher and all five settings pages constructed; no game launched.')
        return 0
    window.show()
    return application.exec()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--worker', action='store_true')
    parser.add_argument('--request')
    parser.add_argument('--smoke-test', action='store_true')
    parser.add_argument('--preview')
    arguments = parser.parse_args()
    return worker(arguments) if arguments.worker else gui(arguments)

if __name__ == '__main__':
    sys.exit(main())
